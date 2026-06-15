using E3Studio.Models;

namespace E3Studio.CAM;

/// <summary>
/// Toolpath calculation engine - generates toolpath moves from geometry
/// Advanced collision avoidance system with dynamic safe heights
/// </summary>
public class ToolpathEngine
{
    // Default safe heights (will be overridden by stock-based calculations)
    private const double DEFAULT_MACHINE_SAFE_Z = 25.0;
    private const double DEFAULT_RAPID_CLEARANCE_Z = 5.0;
    private const double DEFAULT_APPROACH_Z = 2.0;
    
    /// <summary>
    /// Calculate safe heights based on stock and tool
    /// </summary>
    public static SafeHeights CalculateSafeHeights(Stock? stock, Tool? tool)
    {
        double stockThickness = stock?.Thickness ?? 20.0;
        double toolLength = tool?.TotalLength ?? 50.0;
        double toolDiameter = tool?.Diameter ?? 6.0;
        
        // Dynamic safe height calculations
        return new SafeHeights
        {
            // Machine safe: clears stock + tool + safety margin
            MachineSafe = Math.Max(DEFAULT_MACHINE_SAFE_Z, stockThickness * 0.5 + 15),
            
            // Rapid clearance: just above stock surface
            RapidClearance = Math.Max(DEFAULT_RAPID_CLEARANCE_Z, toolDiameter * 0.5 + 2),
            
            // Approach: final approach before plunge
            Approach = DEFAULT_APPROACH_Z,
            
            // Retract increment for chip clearing (based on tool diameter)
            ChipClearRetract = Math.Max(3.0, toolDiameter * 2)
        };
    }
    
    /// <summary>
    /// Safe heights container
    /// </summary>
    public class SafeHeights
    {
        public double MachineSafe { get; set; } = DEFAULT_MACHINE_SAFE_Z;
        public double RapidClearance { get; set; } = DEFAULT_RAPID_CLEARANCE_Z;
        public double Approach { get; set; } = DEFAULT_APPROACH_Z;
        public double ChipClearRetract { get; set; } = 5.0;
    }
    
    /// <summary>
    /// POST-PROCESS: Validate and fix ALL rapid moves to ensure safety
    /// This is the FINAL safety check - catches any moves that might have been missed
    /// </summary>
    public static List<ToolpathMove> EnsureSafeRapids(List<ToolpathMove> moves, SafeHeights heights)
    {
        if (moves.Count < 2) return moves;
        
        var safeMoves = new List<ToolpathMove>();
        ToolpathMove? lastMove = null;
        
        foreach (var move in moves)
        {
            if (lastMove != null)
            {
                // Check if this is an XY movement at unsafe Z height
                bool hasXYMovement = Math.Abs(move.X - lastMove.X) > 0.01 || 
                                     Math.Abs(move.Y - lastMove.Y) > 0.01;
                bool isRapid = move.Type == MoveType.Rapid;
                bool isUnsafeZ = lastMove.Z < heights.MachineSafe - 0.01;
                
                // CRITICAL: If moving XY while at unsafe Z, insert retract first
                if (hasXYMovement && isRapid && isUnsafeZ)
                {
                    // Insert vertical retract BEFORE the XY move
                    safeMoves.Add(new ToolpathMove
                    {
                        Type = MoveType.Rapid,
                        X = lastMove.X,
                        Y = lastMove.Y,
                        Z = heights.MachineSafe
                    });
                }
                
                // Also check: if rapid and Z is below stock surface, that's dangerous
                if (isRapid && move.Z < 0 && hasXYMovement)
                {
                    // This rapid is trying to move XY while INSIDE the stock!
                    // Fix: First retract, then move XY at safe height, then approach
                    safeMoves.Add(new ToolpathMove
                    {
                        Type = MoveType.Rapid,
                        X = lastMove.X,
                        Y = lastMove.Y,
                        Z = heights.MachineSafe
                    });
                    safeMoves.Add(new ToolpathMove
                    {
                        Type = MoveType.Rapid,
                        X = move.X,
                        Y = move.Y,
                        Z = heights.MachineSafe
                    });
                    // Change the move to just go to approach height
                    move.Z = heights.Approach;
                }
            }
            
            safeMoves.Add(move);
            lastMove = move;
        }
        
        return safeMoves;
    }
    
    /// <summary>
    /// Compute toolpath moves for a profile operation
    /// </summary>
    public static List<ToolpathMove> ComputeProfile(List<GeometryPath> paths, Toolpath toolpath, Stock? stock = null)
    {
        var heights = CalculateSafeHeights(stock, toolpath.Tool);
        var moves = new List<ToolpathMove>();
        double toolRadius = (toolpath.Tool?.Diameter ?? 3.175) / 2.0;
        
        foreach (var path in paths)
        {
            if (path is PolyPath poly && poly.Segments.Count > 0)
            {
                // Get transformed points
                var points = GetTransformedPoints(poly);
                if (points.Count < 2) continue;
                
                // Offset the path based on side
                var offsetPoints = OffsetPath(points, toolRadius, toolpath.Side, poly.IsClosed);
                
                if (offsetPoints.Count > 0)
                {
                    // Generate profile for each depth pass
                    double currentDepth = 0;
                    
                    // SAFE: Rapid to MACHINE SAFE HEIGHT before XY move
                    moves.Add(new ToolpathMove
                    {
                        Type = MoveType.Rapid,
                        X = 0,
                        Y = 0,
                        Z = heights.MachineSafe
                    });
                    
                    // Rapid move to start XY position at safe height
                    moves.Add(new ToolpathMove
                    {
                        Type = MoveType.Rapid,
                        X = offsetPoints[0].X,
                        Y = offsetPoints[0].Y,
                        Z = heights.MachineSafe
                    });
                    
                    // Drop to rapid clearance height
                    moves.Add(new ToolpathMove
                    {
                        Type = MoveType.Rapid,
                        X = offsetPoints[0].X,
                        Y = offsetPoints[0].Y,
                        Z = heights.RapidClearance
                    });
                    
                    while (currentDepth < toolpath.FinalDepth)
                    {
                        currentDepth += toolpath.CutDepth;
                        if (currentDepth > toolpath.FinalDepth) currentDepth = toolpath.FinalDepth;
                        
                        // Plunge to depth
                        moves.Add(new ToolpathMove
                        {
                            Type = MoveType.Linear,
                            X = offsetPoints[0].X,
                            Y = offsetPoints[0].Y,
                            Z = -currentDepth,
                            F = toolpath.PlungeRate
                        });
                        
                        // Cut around the profile
                        for (int i = 1; i < offsetPoints.Count; i++)
                        {
                            moves.Add(new ToolpathMove
                            {
                                Type = MoveType.Linear,
                                X = offsetPoints[i].X,
                                Y = offsetPoints[i].Y,
                                Z = -currentDepth,
                                F = toolpath.FeedRate
                            });
                        }
                        
                        // Close the path if needed
                        if (poly.IsClosed && offsetPoints.Count > 2)
                        {
                            moves.Add(new ToolpathMove
                            {
                                Type = MoveType.Linear,
                                X = offsetPoints[0].X,
                                Y = offsetPoints[0].Y,
                                Z = -currentDepth,
                                F = toolpath.FeedRate
                            });
                        }
                        
                        // Retract after each pass
                        moves.Add(new ToolpathMove
                        {
                            Type = MoveType.Rapid,
                            X = offsetPoints[0].X,
                            Y = offsetPoints[0].Y,
                            Z = heights.RapidClearance
                        });
                    }
                    
                    // Final retract to MACHINE SAFE HEIGHT
                    moves.Add(new ToolpathMove
                    {
                        Type = MoveType.Rapid,
                        X = offsetPoints[0].X,
                        Y = offsetPoints[0].Y,
                        Z = heights.MachineSafe
                    });
                }
            }
        }
        
        // Add tabs if enabled
        if (toolpath.TabCount > 0)
        {
            AddTabs(moves, toolpath);
        }
        
        // POST-PROCESS: Final safety validation
        return EnsureSafeRapids(moves, heights);
    }
    
    /// <summary>
    /// Compute toolpath moves for a pocket operation
    /// </summary>
    public static List<ToolpathMove> ComputePocket(List<GeometryPath> paths, Toolpath toolpath, Stock? stock = null)
    {
        var heights = CalculateSafeHeights(stock, toolpath.Tool);
        var moves = new List<ToolpathMove>();
        double toolDiameter = toolpath.Tool?.Diameter ?? 3.175;
        double toolRadius = toolDiameter / 2.0;
        double stepOver = toolDiameter * (toolpath.StepOver / 100.0);
        
        foreach (var path in paths)
        {
            if (path is PolyPath poly && poly.Segments.Count > 0)
            {
                var points = GetTransformedPoints(poly);
                if (points.Count < 3) continue;
                
                // Get bounding box
                var bounds = GetBounds(points);
                
                // Generate pocket for each depth pass
                double currentDepth = 0;
                while (currentDepth < toolpath.FinalDepth)
                {
                    currentDepth += toolpath.CutDepth;
                    if (currentDepth > toolpath.FinalDepth) currentDepth = toolpath.FinalDepth;
                    
                    // Generate zigzag pattern for this depth with proper safe heights
                    var pocketMoves = GenerateZigzagPocket(points, bounds, stepOver, toolRadius, currentDepth, toolpath.FeedRate, toolpath.PlungeRate, heights);
                    moves.AddRange(pocketMoves);
                }
            }
        }
        
        // Final retract to MACHINE SAFE HEIGHT
        if (moves.Count > 0)
        {
            moves.Add(new ToolpathMove { Type = MoveType.Rapid, X = moves.Last().X, Y = moves.Last().Y, Z = heights.MachineSafe });
        }
        
        // POST-PROCESS: Final safety validation
        return EnsureSafeRapids(moves, heights);
    }
    
    /// <summary>
    /// Compute toolpath moves for a drill operation
    /// CRITICAL: Full retract to MACHINE SAFE before every XY move!
    /// </summary>
    public static List<ToolpathMove> ComputeDrill(List<GeometryPath> paths, Toolpath toolpath, Stock? stock = null)
    {
        var heights = CalculateSafeHeights(stock, toolpath.Tool);
        var moves = new List<ToolpathMove>();
        double peckDepth = toolpath.CutDepth > 0 ? toolpath.CutDepth : toolpath.FinalDepth;
        
        // Collect all drill points
        var drillPoints = new List<Point2D>();
        
        foreach (var path in paths)
        {
            if (path is PolyPath poly && poly.Segments.Count > 0)
            {
                var points = GetTransformedPoints(poly);
                
                // For closed paths, drill at centroid
                if (poly.IsClosed && points.Count >= 3)
                {
                    drillPoints.Add(GetCentroid(points));
                }
                // For open paths, drill at first point only (or could be circle center)
                else if (points.Count > 0)
                {
                    // Check if it's a circle (all points equidistant from center)
                    var centroid = GetCentroid(points);
                    drillPoints.Add(centroid);
                }
            }
        }
        
        // FIRST: Start at machine safe height
        if (drillPoints.Count > 0)
        {
            moves.Add(new ToolpathMove 
            { 
                Type = MoveType.Rapid, 
                X = drillPoints[0].X, 
                Y = drillPoints[0].Y, 
                Z = heights.MachineSafe 
            });
        }
        
        // Generate drill cycles for each point
        foreach (var pt in drillPoints)
        {
            // ═══════════════════════════════════════════════════════════
            // CRITICAL SAFETY: Full retract BEFORE moving to hole position
            // ═══════════════════════════════════════════════════════════
            moves.Add(new ToolpathMove 
            { 
                Type = MoveType.Rapid, 
                X = moves.Last().X, 
                Y = moves.Last().Y, 
                Z = heights.MachineSafe  // FULL HEIGHT!
            });
            
            // Now safe to move XY
            moves.Add(new ToolpathMove 
            { 
                Type = MoveType.Rapid, 
                X = pt.X, 
                Y = pt.Y, 
                Z = heights.MachineSafe 
            });
            
            // Rapid to clearance height
            moves.Add(new ToolpathMove 
            { 
                Type = MoveType.Rapid, 
                X = pt.X, 
                Y = pt.Y, 
                Z = heights.RapidClearance
            });
            
            // Rapid to approach height (just above surface)
            moves.Add(new ToolpathMove 
            { 
                Type = MoveType.Rapid, 
                X = pt.X, 
                Y = pt.Y, 
                Z = heights.Approach
            });
            
            // Peck drilling cycle
            double currentDepth = 0;
            while (currentDepth < toolpath.FinalDepth)
            {
                currentDepth += peckDepth;
                if (currentDepth > toolpath.FinalDepth) currentDepth = toolpath.FinalDepth;
                
                // Plunge to depth
                moves.Add(new ToolpathMove 
                { 
                    Type = MoveType.Linear, 
                    X = pt.X, 
                    Y = pt.Y, 
                    Z = -currentDepth,
                    F = toolpath.PlungeRate
                });
                
                // Retract for chip clearing (if not at final depth)
                if (currentDepth < toolpath.FinalDepth)
                {
                    // Retract to clearance height for chip clearing
                    moves.Add(new ToolpathMove 
                    { 
                        Type = MoveType.Rapid, 
                        X = pt.X, 
                        Y = pt.Y, 
                        Z = heights.RapidClearance
                    });
                    
                    // Rapid back to just above current hole depth
                    double reApproach = -currentDepth + 1.0;
                    moves.Add(new ToolpathMove 
                    { 
                        Type = MoveType.Rapid, 
                        X = pt.X, 
                        Y = pt.Y, 
                        Z = reApproach
                    });
                }
            }
            
            // ═══════════════════════════════════════════════════════════
            // Final retract to MACHINE SAFE HEIGHT after hole
            // ═══════════════════════════════════════════════════════════
            moves.Add(new ToolpathMove 
            { 
                Type = MoveType.Rapid, 
                X = pt.X, 
                Y = pt.Y, 
                Z = heights.MachineSafe  // FULL HEIGHT!
            });
        }
        
        // POST-PROCESS: Final safety validation
        return EnsureSafeRapids(moves, heights);
    }
    
    #region Helper Methods
    
    private static List<Point2D> GetTransformedPoints(PolyPath poly)
    {
        var points = new List<Point2D>();
        var transform = E3Studio.Services.Matrix3x2.Identity *
                        E3Studio.Services.Matrix3x2.CreateScale(poly.ScaleX, poly.ScaleY) *
                        E3Studio.Services.Matrix3x2.CreateRotation(poly.Rotation * Math.PI / 180.0) *
                        E3Studio.Services.Matrix3x2.CreateTranslation(poly.X, poly.Y);
        
        foreach (var seg in poly.Segments)
        {
            var pt = new Point2D(seg.EndPoint.X, seg.EndPoint.Y);
            pt = transform.Transform(pt);
            points.Add(pt);
        }
        
        return points;
    }
    
    private static List<Point2D> OffsetPath(List<Point2D> points, double offset, ProfileSide side, bool isClosed)
    {
        if (points.Count < 2) return points;
        if (side == ProfileSide.OnLine || offset <= 0) return points;
        
        // Determine offset direction
        double sign = side == ProfileSide.Outside ? 1 : -1;
        
        // Check winding direction for closed paths
        if (isClosed)
        {
            double area = GetSignedArea(points);
            if (area < 0) sign = -sign; // Clockwise path
        }
        
        var offsetPoints = new List<Point2D>();
        int n = points.Count;
        
        for (int i = 0; i < n; i++)
        {
            var prev = points[(i - 1 + n) % n];
            var curr = points[i];
            var next = points[(i + 1) % n];
            
            // Calculate normals
            var n1 = GetNormal(prev, curr);
            var n2 = GetNormal(curr, next);
            
            // Average normal at vertex
            double nx = (n1.X + n2.X) / 2;
            double ny = (n1.Y + n2.Y) / 2;
            double len = Math.Sqrt(nx * nx + ny * ny);
            
            if (len > 0.0001)
            {
                nx /= len;
                ny /= len;
                
                // Offset point
                offsetPoints.Add(new Point2D(
                    curr.X + nx * offset * sign,
                    curr.Y + ny * offset * sign
                ));
            }
            else
            {
                offsetPoints.Add(new Point2D(curr.X, curr.Y));
            }
        }
        
        return offsetPoints;
    }
    
    private static (double X, double Y) GetNormal(Point2D p1, Point2D p2)
    {
        double dx = p2.X - p1.X;
        double dy = p2.Y - p1.Y;
        double len = Math.Sqrt(dx * dx + dy * dy);
        
        if (len < 0.0001) return (0, 0);
        
        // Perpendicular (rotated 90°)
        return (-dy / len, dx / len);
    }
    
    private static double GetSignedArea(List<Point2D> points)
    {
        double area = 0;
        int n = points.Count;
        
        for (int i = 0; i < n; i++)
        {
            var p1 = points[i];
            var p2 = points[(i + 1) % n];
            area += (p2.X - p1.X) * (p2.Y + p1.Y);
        }
        
        return area / 2;
    }
    
    private static (double minX, double minY, double maxX, double maxY) GetBounds(List<Point2D> points)
    {
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        
        foreach (var pt in points)
        {
            minX = Math.Min(minX, pt.X);
            minY = Math.Min(minY, pt.Y);
            maxX = Math.Max(maxX, pt.X);
            maxY = Math.Max(maxY, pt.Y);
        }
        
        return (minX, minY, maxX, maxY);
    }
    
    private static Point2D GetCentroid(List<Point2D> points)
    {
        double cx = 0, cy = 0;
        foreach (var pt in points)
        {
            cx += pt.X;
            cy += pt.Y;
        }
        return new Point2D(cx / points.Count, cy / points.Count);
    }
    
    private static List<ToolpathMove> GenerateZigzagPocket(List<Point2D> boundary, 
        (double minX, double minY, double maxX, double maxY) bounds, 
        double stepOver, double toolRadius, double depth, double feedRate, double plungeRate, SafeHeights heights)
    {
        var moves = new List<ToolpathMove>();
        
        // Inset the boundary by tool radius
        double inset = toolRadius * 1.1; // Slight extra inset
        double startX = bounds.minX + inset;
        double endX = bounds.maxX - inset;
        double startY = bounds.minY + inset;
        double endY = bounds.maxY - inset;
        
        if (startX >= endX || startY >= endY) return moves;
        
        bool goingRight = true;
        double y = startY;
        bool firstMove = true;
        
        while (y <= endY)
        {
            // Find intersection points with boundary for this Y
            double leftX = startX;
            double rightX = endX;
            
            // Clip to actual boundary
            var intersections = GetHorizontalIntersections(y, boundary);
            if (intersections.Count >= 2)
            {
                intersections.Sort();
                leftX = Math.Max(intersections[0] + toolRadius, startX);
                rightX = Math.Min(intersections[intersections.Count - 1] - toolRadius, endX);
            }
            
            if (leftX < rightX)
            {
                if (goingRight)
                {
                    if (firstMove)
                    {
                        // SAFE: Full retract, move XY, then approach
                        moves.Add(new ToolpathMove { Type = MoveType.Rapid, X = leftX, Y = y, Z = heights.MachineSafe });
                        moves.Add(new ToolpathMove { Type = MoveType.Rapid, X = leftX, Y = y, Z = heights.RapidClearance });
                        // Plunge
                        moves.Add(new ToolpathMove { Type = MoveType.Linear, X = leftX, Y = y, Z = -depth, F = plungeRate });
                        firstMove = false;
                    }
                    else
                    {
                        // Move to line start
                        moves.Add(new ToolpathMove { Type = MoveType.Linear, X = leftX, Y = y, Z = -depth, F = feedRate });
                    }
                    // Cut across
                    moves.Add(new ToolpathMove { Type = MoveType.Linear, X = rightX, Y = y, Z = -depth, F = feedRate });
                }
                else
                {
                    // Move to line start
                    moves.Add(new ToolpathMove { Type = MoveType.Linear, X = rightX, Y = y, Z = -depth, F = feedRate });
                    // Cut across
                    moves.Add(new ToolpathMove { Type = MoveType.Linear, X = leftX, Y = y, Z = -depth, F = feedRate });
                }
            }
            
            y += stepOver;
            
            if (y <= endY && moves.Count > 0)
            {
                // Step over - stay at depth
                var lastMove = moves.Last();
                moves.Add(new ToolpathMove { Type = MoveType.Linear, X = lastMove.X, Y = y, Z = -depth, F = feedRate });
            }
            
            goingRight = !goingRight;
        }
        
        // Retract at end of this depth pass
        if (moves.Count > 0)
        {
            var lastMove = moves.Last();
            moves.Add(new ToolpathMove { Type = MoveType.Rapid, X = lastMove.X, Y = lastMove.Y, Z = heights.RapidClearance });
        }
        
        return moves;
    }
    
    private static List<double> GetHorizontalIntersections(double y, List<Point2D> polygon)
    {
        var intersections = new List<double>();
        int n = polygon.Count;
        
        for (int i = 0; i < n; i++)
        {
            var p1 = polygon[i];
            var p2 = polygon[(i + 1) % n];
            
            // Check if edge crosses this Y
            if ((p1.Y <= y && p2.Y > y) || (p2.Y <= y && p1.Y > y))
            {
                // Calculate X intersection
                double t = (y - p1.Y) / (p2.Y - p1.Y);
                double x = p1.X + t * (p2.X - p1.X);
                intersections.Add(x);
            }
        }
        
        return intersections;
    }
    
    private static bool IsPointInPolygon(double x, double y, List<Point2D> polygon)
    {
        bool inside = false;
        int n = polygon.Count;
        
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            var pi = polygon[i];
            var pj = polygon[j];
            
            if ((pi.Y > y) != (pj.Y > y) &&
                x < (pj.X - pi.X) * (y - pi.Y) / (pj.Y - pi.Y) + pi.X)
            {
                inside = !inside;
            }
        }
        
        return inside;
    }
    
    private static void AddTabs(List<ToolpathMove> moves, Toolpath toolpath)
    {
        if (moves.Count < 2 || toolpath.TabCount <= 0) return;
        
        // Calculate total path length
        double totalLength = 0;
        for (int i = 1; i < moves.Count; i++)
        {
            var prev = moves[i - 1];
            var curr = moves[i];
            totalLength += Math.Sqrt(Math.Pow(curr.X - prev.X, 2) + Math.Pow(curr.Y - prev.Y, 2));
        }
        
        // Tab spacing
        double tabSpacing = totalLength / toolpath.TabCount;
        double tabHalfWidth = toolpath.TabWidth / 2;
        
        // Mark moves that are within tab zones
        double accumulated = 0;
        int tabIndex = 0;
        double nextTabCenter = tabSpacing / 2;
        
        for (int i = 1; i < moves.Count && tabIndex < toolpath.TabCount; i++)
        {
            var prev = moves[i - 1];
            var curr = moves[i];
            double segLength = Math.Sqrt(Math.Pow(curr.X - prev.X, 2) + Math.Pow(curr.Y - prev.Y, 2));
            
            // Check if tab center falls within this segment
            if (accumulated + segLength >= nextTabCenter)
            {
                // Raise Z for tab
                curr.Z = -toolpath.FinalDepth + toolpath.TabHeight;
                
                tabIndex++;
                nextTabCenter = tabSpacing / 2 + tabIndex * tabSpacing;
            }
            
            accumulated += segLength;
        }
    }
    
    #endregion
    
    #region V-Carve Toolpath
    
    /// <summary>
    /// Compute V-Carve toolpath for text and detailed engravings
    /// V-bit depth varies based on path width
    /// </summary>
    public static List<ToolpathMove> ComputeVCarve(List<GeometryPath> paths, Toolpath toolpath, Stock? stock = null)
    {
        var heights = CalculateSafeHeights(stock, toolpath.Tool);
        var moves = new List<ToolpathMove>();
        
        // V-bit parameters
        double vAngle = toolpath.Tool?.VAngle ?? 60.0;
        double halfAngle = vAngle / 2.0 * Math.PI / 180.0;
        double maxDepth = toolpath.VCarveMaxDepth > 0 ? toolpath.VCarveMaxDepth : toolpath.FinalDepth;
        double flatDepth = toolpath.VCarveFlatDepth;
        
        foreach (var path in paths)
        {
            if (path is PolyPath poly && poly.Segments.Count > 0)
            {
                var points = GetTransformedPoints(poly);
                if (points.Count < 2) continue;
                
                // Start position - machine safe
                moves.Add(new ToolpathMove
                {
                    Type = MoveType.Rapid,
                    X = points[0].X,
                    Y = points[0].Y,
                    Z = heights.MachineSafe
                });
                
                // Rapid to clearance
                moves.Add(new ToolpathMove
                {
                    Type = MoveType.Rapid,
                    X = points[0].X,
                    Y = points[0].Y,
                    Z = heights.RapidClearance
                });
                
                // V-carve along each point
                for (int i = 0; i < points.Count; i++)
                {
                    var pt = points[i];
                    
                    // Calculate depth based on path width (distance to nearest edge)
                    double pathWidth = CalculatePathWidth(pt, points, i);
                    double depth = Math.Min(pathWidth / (2 * Math.Tan(halfAngle)), maxDepth);
                    
                    // Add flat bottom if specified
                    if (flatDepth > 0 && depth > flatDepth)
                    {
                        depth = flatDepth;
                    }
                    
                    // Carve move
                    moves.Add(new ToolpathMove
                    {
                        Type = MoveType.Linear,
                        X = pt.X,
                        Y = pt.Y,
                        Z = -depth,
                        F = toolpath.FeedRate
                    });
                }
                
                // Retract
                moves.Add(new ToolpathMove
                {
                    Type = MoveType.Rapid,
                    X = points.Last().X,
                    Y = points.Last().Y,
                    Z = heights.MachineSafe
                });
            }
        }
        
        return EnsureSafeRapids(moves, heights);
    }
    
    /// <summary>
    /// Calculate path width at a point (for V-carve depth calculation)
    /// </summary>
    private static double CalculatePathWidth(Point2D pt, List<Point2D> points, int index)
    {
        // Simple approach: use distance to neighboring segments
        double minDist = double.MaxValue;
        
        int n = points.Count;
        for (int i = 0; i < n - 1; i++)
        {
            if (Math.Abs(i - index) <= 1) continue; // Skip adjacent segments
            
            double dist = PointToSegmentDistance(pt, points[i], points[i + 1]);
            minDist = Math.Min(minDist, dist);
        }
        
        // Default width if no opposing edge found
        return minDist == double.MaxValue ? 2.0 : minDist * 2;
    }
    
    /// <summary>
    /// Distance from point to line segment
    /// </summary>
    private static double PointToSegmentDistance(Point2D p, Point2D a, Point2D b)
    {
        double dx = b.X - a.X;
        double dy = b.Y - a.Y;
        double lenSq = dx * dx + dy * dy;
        
        if (lenSq < 0.0001) return Distance(p, a);
        
        double t = Math.Max(0, Math.Min(1, ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lenSq));
        
        double projX = a.X + t * dx;
        double projY = a.Y + t * dy;
        
        return Math.Sqrt(Math.Pow(p.X - projX, 2) + Math.Pow(p.Y - projY, 2));
    }
    
    private static double Distance(Point2D a, Point2D b)
    {
        return Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));
    }
    
    #endregion
    
    #region Lead-In / Lead-Out
    
    /// <summary>
    /// Generate lead-in moves for smooth entry into cut
    /// </summary>
    public static List<ToolpathMove> GenerateLeadIn(Point2D startPt, Point2D nextPt, 
        double depth, double radius, LeadType type, double feedRate, double plungeRate)
    {
        var moves = new List<ToolpathMove>();
        if (type == LeadType.None || radius <= 0) return moves;
        
        // Direction from start to next point
        double dx = nextPt.X - startPt.X;
        double dy = nextPt.Y - startPt.Y;
        double len = Math.Sqrt(dx * dx + dy * dy);
        
        if (len < 0.001) return moves;
        
        // Normalize
        dx /= len;
        dy /= len;
        
        // Perpendicular (left)
        double px = -dy;
        double py = dx;
        
        switch (type)
        {
            case LeadType.Line:
                // Straight line approach from behind
                double lineX = startPt.X - dx * radius;
                double lineY = startPt.Y - dy * radius;
                
                // Move to lead-in start at depth
                moves.Add(new ToolpathMove
                {
                    Type = MoveType.Linear,
                    X = lineX,
                    Y = lineY,
                    Z = -depth,
                    F = plungeRate
                });
                
                // Line into start point
                moves.Add(new ToolpathMove
                {
                    Type = MoveType.Linear,
                    X = startPt.X,
                    Y = startPt.Y,
                    Z = -depth,
                    F = feedRate
                });
                break;
                
            case LeadType.Arc:
            case LeadType.Tangent:
                // Arc lead-in (90° or tangent)
                double arcCenterX = startPt.X - dx * radius;
                double arcCenterY = startPt.Y - dy * radius;
                
                // Arc start point (perpendicular to entry)
                double arcStartX = arcCenterX + px * radius;
                double arcStartY = arcCenterY + py * radius;
                
                // Plunge at arc start
                moves.Add(new ToolpathMove
                {
                    Type = MoveType.Linear,
                    X = arcStartX,
                    Y = arcStartY,
                    Z = -depth,
                    F = plungeRate
                });
                
                // Arc to start point (G3 - CCW)
                moves.Add(new ToolpathMove
                {
                    Type = MoveType.ArcCCW,
                    X = startPt.X,
                    Y = startPt.Y,
                    Z = -depth,
                    I = arcCenterX - arcStartX,
                    J = arcCenterY - arcStartY,
                    F = feedRate
                });
                break;
        }
        
        return moves;
    }
    
    /// <summary>
    /// Generate lead-out moves for smooth exit from cut
    /// </summary>
    public static List<ToolpathMove> GenerateLeadOut(Point2D endPt, Point2D prevPt,
        double depth, double radius, LeadType type, double feedRate)
    {
        var moves = new List<ToolpathMove>();
        if (type == LeadType.None || radius <= 0) return moves;
        
        // Direction from prev to end point
        double dx = endPt.X - prevPt.X;
        double dy = endPt.Y - prevPt.Y;
        double len = Math.Sqrt(dx * dx + dy * dy);
        
        if (len < 0.001) return moves;
        
        dx /= len;
        dy /= len;
        
        double px = -dy;
        double py = dx;
        
        switch (type)
        {
            case LeadType.Line:
                // Straight line exit
                moves.Add(new ToolpathMove
                {
                    Type = MoveType.Linear,
                    X = endPt.X + dx * radius,
                    Y = endPt.Y + dy * radius,
                    Z = -depth,
                    F = feedRate
                });
                break;
                
            case LeadType.Arc:
            case LeadType.Tangent:
                // Arc lead-out
                double arcCenterX = endPt.X + dx * radius;
                double arcCenterY = endPt.Y + dy * radius;
                
                double arcEndX = arcCenterX + px * radius;
                double arcEndY = arcCenterY + py * radius;
                
                moves.Add(new ToolpathMove
                {
                    Type = MoveType.ArcCW,
                    X = arcEndX,
                    Y = arcEndY,
                    Z = -depth,
                    I = arcCenterX - endPt.X,
                    J = arcCenterY - endPt.Y,
                    F = feedRate
                });
                break;
        }
        
        return moves;
    }
    
    #endregion
    
    #region Helix / Ramp Entry
    
    /// <summary>
    /// Generate helical entry into material
    /// Better for tool life - gradual spiral down instead of plunging
    /// </summary>
    public static List<ToolpathMove> GenerateHelixEntry(Point2D center, double startZ, double endZ,
        double helixDiameter, double helixAngle, double feedRate)
    {
        var moves = new List<ToolpathMove>();
        
        double radius = helixDiameter / 2.0;
        double totalDepth = startZ - endZ;
        
        if (totalDepth <= 0 || radius <= 0) return moves;
        
        // Calculate helix parameters
        double circumference = 2 * Math.PI * radius;
        double depthPerRev = circumference * Math.Tan(helixAngle * Math.PI / 180.0);
        int fullRevolutions = (int)Math.Ceiling(totalDepth / depthPerRev);
        
        // Minimum 1 revolution for smooth entry
        fullRevolutions = Math.Max(1, fullRevolutions);
        depthPerRev = totalDepth / fullRevolutions;
        
        // Generate helix as series of arcs
        int segmentsPerRev = 4; // 4 quarter arcs per revolution
        double angleStep = Math.PI / 2; // 90 degrees
        double zStep = depthPerRev / segmentsPerRev;
        
        double currentZ = startZ;
        double currentAngle = 0;
        
        // Start position
        double startX = center.X + radius;
        double startY = center.Y;
        
        moves.Add(new ToolpathMove
        {
            Type = MoveType.Rapid,
            X = startX,
            Y = startY,
            Z = startZ
        });
        
        // Generate helix segments
        for (int rev = 0; rev < fullRevolutions; rev++)
        {
            for (int seg = 0; seg < segmentsPerRev; seg++)
            {
                currentAngle += angleStep;
                currentZ -= zStep;
                
                if (currentZ < endZ) currentZ = endZ;
                
                double endX = center.X + radius * Math.Cos(currentAngle);
                double endY = center.Y + radius * Math.Sin(currentAngle);
                
                // Arc center offset from current position
                double prevX = center.X + radius * Math.Cos(currentAngle - angleStep);
                double prevY = center.Y + radius * Math.Sin(currentAngle - angleStep);
                
                moves.Add(new ToolpathMove
                {
                    Type = MoveType.ArcCW,
                    X = endX,
                    Y = endY,
                    Z = currentZ,
                    I = center.X - prevX,
                    J = center.Y - prevY,
                    F = feedRate
                });
            }
        }
        
        // Final move to center at full depth
        moves.Add(new ToolpathMove
        {
            Type = MoveType.Linear,
            X = center.X,
            Y = center.Y,
            Z = endZ,
            F = feedRate
        });
        
        return moves;
    }
    
    /// <summary>
    /// Generate ramped entry into material
    /// Linear ramped approach - simpler than helix
    /// </summary>
    public static List<ToolpathMove> GenerateRampEntry(Point2D startPt, Point2D endPt,
        double startZ, double endZ, double rampAngle, double feedRate)
    {
        var moves = new List<ToolpathMove>();
        
        double totalDepth = startZ - endZ;
        if (totalDepth <= 0) return moves;
        
        // Calculate ramp distance needed
        double rampDistance = totalDepth / Math.Tan(rampAngle * Math.PI / 180.0);
        
        // Direction from start to end
        double dx = endPt.X - startPt.X;
        double dy = endPt.Y - startPt.Y;
        double pathLength = Math.Sqrt(dx * dx + dy * dy);
        
        if (pathLength < 0.001) return moves;
        
        dx /= pathLength;
        dy /= pathLength;
        
        // If path is shorter than ramp distance, multiple passes needed
        int passes = (int)Math.Ceiling(rampDistance / pathLength);
        double zStepPerPass = totalDepth / passes;
        
        double currentZ = startZ;
        bool forward = true;
        
        for (int pass = 0; pass < passes; pass++)
        {
            double targetZ = currentZ - zStepPerPass;
            if (targetZ < endZ) targetZ = endZ;
            
            if (forward)
            {
                // Ramp from start to end
                moves.Add(new ToolpathMove
                {
                    Type = MoveType.Linear,
                    X = endPt.X,
                    Y = endPt.Y,
                    Z = targetZ,
                    F = feedRate
                });
            }
            else
            {
                // Ramp from end to start
                moves.Add(new ToolpathMove
                {
                    Type = MoveType.Linear,
                    X = startPt.X,
                    Y = startPt.Y,
                    Z = targetZ,
                    F = feedRate
                });
            }
            
            currentZ = targetZ;
            forward = !forward;
        }
        
        return moves;
    }
    
    #endregion
    
    #region Arc Fitting
    
    /// <summary>
    /// Fit arcs to linear segments where possible
    /// Reduces G-Code size and improves surface finish
    /// </summary>
    public static List<ToolpathMove> FitArcsToPath(List<ToolpathMove> moves, double tolerance = 0.01)
    {
        if (moves.Count < 3) return moves;
        
        var result = new List<ToolpathMove>();
        int i = 0;
        
        while (i < moves.Count)
        {
            // Only try to fit linear moves at same Z
            if (i + 2 < moves.Count && 
                moves[i].Type == MoveType.Linear &&
                moves[i + 1].Type == MoveType.Linear &&
                moves[i + 2].Type == MoveType.Linear &&
                Math.Abs(moves[i].Z - moves[i + 1].Z) < 0.001 &&
                Math.Abs(moves[i + 1].Z - moves[i + 2].Z) < 0.001)
            {
                // Try to fit an arc through these three points
                var p1 = new Point2D(moves[i].X, moves[i].Y);
                var p2 = new Point2D(moves[i + 1].X, moves[i + 1].Y);
                var p3 = new Point2D(moves[i + 2].X, moves[i + 2].Y);
                
                var arcResult = TryFitArc(p1, p2, p3, tolerance);
                
                if (arcResult.success)
                {
                    // Add first point if not already added
                    if (result.Count == 0 || 
                        Math.Abs(result.Last().X - p1.X) > 0.001 ||
                        Math.Abs(result.Last().Y - p1.Y) > 0.001)
                    {
                        result.Add(moves[i]);
                    }
                    
                    // Add arc move
                    result.Add(new ToolpathMove
                    {
                        Type = arcResult.clockwise ? MoveType.ArcCW : MoveType.ArcCCW,
                        X = p3.X,
                        Y = p3.Y,
                        Z = moves[i + 2].Z,
                        I = arcResult.centerX - p1.X,
                        J = arcResult.centerY - p1.Y,
                        F = moves[i + 1].F
                    });
                    
                    i += 3;
                    continue;
                }
            }
            
            result.Add(moves[i]);
            i++;
        }
        
        return result;
    }
    
    /// <summary>
    /// Try to fit an arc through three points
    /// </summary>
    private static (bool success, double centerX, double centerY, double radius, bool clockwise) 
        TryFitArc(Point2D p1, Point2D p2, Point2D p3, double tolerance)
    {
        // Calculate circle center through three points
        double ax = p1.X, ay = p1.Y;
        double bx = p2.X, by = p2.Y;
        double cx = p3.X, cy = p3.Y;
        
        double d = 2 * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by));
        
        if (Math.Abs(d) < 0.0001)
        {
            // Points are collinear
            return (false, 0, 0, 0, false);
        }
        
        double ux = ((ax * ax + ay * ay) * (by - cy) + 
                     (bx * bx + by * by) * (cy - ay) + 
                     (cx * cx + cy * cy) * (ay - by)) / d;
        
        double uy = ((ax * ax + ay * ay) * (cx - bx) + 
                     (bx * bx + by * by) * (ax - cx) + 
                     (cx * cx + cy * cy) * (bx - ax)) / d;
        
        double radius = Math.Sqrt(Math.Pow(ax - ux, 2) + Math.Pow(ay - uy, 2));
        
        // Check if arc is reasonable (not too large)
        if (radius > 1000 || radius < tolerance)
        {
            return (false, 0, 0, 0, false);
        }
        
        // Verify middle point is on the arc within tolerance
        double distToCenter = Math.Sqrt(Math.Pow(bx - ux, 2) + Math.Pow(by - uy, 2));
        if (Math.Abs(distToCenter - radius) > tolerance)
        {
            return (false, 0, 0, 0, false);
        }
        
        // Determine arc direction (CW or CCW)
        double cross = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
        bool clockwise = cross < 0;
        
        return (true, ux, uy, radius, clockwise);
    }
    
    #endregion
    
    #region Apply Entry Mode to Profile
    
    /// <summary>
    /// Apply selected entry mode to a profile toolpath
    /// </summary>
    public static List<ToolpathMove> ApplyEntryMode(List<ToolpathMove> moves, Toolpath toolpath, 
        Point2D entryPoint, double depth, SafeHeights heights)
    {
        var result = new List<ToolpathMove>();
        
        // Find the first plunge/cut move
        int firstCutIndex = moves.FindIndex(m => m.Type == MoveType.Linear && m.Z < 0);
        if (firstCutIndex < 0) return moves;
        
        // Add moves before the cut
        for (int i = 0; i < firstCutIndex; i++)
        {
            result.Add(moves[i]);
        }
        
        // Apply entry based on mode
        switch (toolpath.EntryMode)
        {
            case EntryMode.Helix:
                var helixMoves = GenerateHelixEntry(
                    entryPoint,
                    heights.RapidClearance,
                    -depth,
                    toolpath.HelixDiameter,
                    toolpath.HelixAngle,
                    toolpath.PlungeRate
                );
                result.AddRange(helixMoves);
                break;
                
            case EntryMode.Ramp:
                // Find second point for ramp direction
                var nextPt = firstCutIndex + 1 < moves.Count 
                    ? new Point2D(moves[firstCutIndex + 1].X, moves[firstCutIndex + 1].Y)
                    : entryPoint;
                    
                var rampMoves = GenerateRampEntry(
                    entryPoint,
                    nextPt,
                    heights.RapidClearance,
                    -depth,
                    toolpath.RampAngle,
                    toolpath.PlungeRate
                );
                result.AddRange(rampMoves);
                break;
                
            case EntryMode.Plunge:
            default:
                // Standard plunge - add the original cut move
                result.Add(moves[firstCutIndex]);
                break;
        }
        
        // Add lead-in if configured
        if (toolpath.LeadInRadius > 0 && toolpath.LeadInType != LeadType.None)
        {
            var nextPt = firstCutIndex + 1 < moves.Count 
                ? new Point2D(moves[firstCutIndex + 1].X, moves[firstCutIndex + 1].Y)
                : entryPoint;
                
            var leadInMoves = GenerateLeadIn(
                entryPoint, nextPt, depth,
                toolpath.LeadInRadius,
                toolpath.LeadInType,
                toolpath.FeedRate,
                toolpath.PlungeRate
            );
            result.AddRange(leadInMoves);
        }
        
        // Add remaining moves
        for (int i = firstCutIndex + 1; i < moves.Count; i++)
        {
            result.Add(moves[i]);
        }
        
        // Add lead-out if configured
        if (toolpath.LeadOutRadius > 0 && toolpath.LeadOutType != LeadType.None && result.Count >= 2)
        {
            var lastCut = result.Last();
            var prevMove = result[result.Count - 2];
            
            var leadOutMoves = GenerateLeadOut(
                new Point2D(lastCut.X, lastCut.Y),
                new Point2D(prevMove.X, prevMove.Y),
                depth,
                toolpath.LeadOutRadius,
                toolpath.LeadOutType,
                toolpath.FeedRate
            );
            result.AddRange(leadOutMoves);
        }
        
        // Apply arc fitting if enabled
        if (toolpath.UseArcFitting)
        {
            result = FitArcsToPath(result, toolpath.ArcTolerance);
        }
        
        return result;
    }
    
    #endregion
}
