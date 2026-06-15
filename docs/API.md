# WebSocket API Reference

E3Studio backend exposes a WebSocket API on `ws://localhost:9001` for communication with the Web UI.

## Protocol

All messages are JSON-encoded with the following base structure:

```json
{
  "type": "message.type",
  "id": "unique-request-id",
  "payload": { ... }
}
```

### Response Format

```json
{
  "type": "message.type.response",
  "id": "matching-request-id",
  "status": "ok" | "error",
  "payload": { ... }
}
```

### Error Response

```json
{
  "type": "error",
  "id": "matching-request-id",
  "status": "error",
  "error": {
    "code": "ERROR_CODE",
    "message": "Human-readable error description"
  }
}
```

## Message Types

### Project

#### `project.new`
Create a new project.

```json
// Request
{
  "type": "project.new",
  "id": "1",
  "payload": {
    "name": "My Project",
    "stock": {
      "width": 300,
      "height": 200,
      "thickness": 18,
      "material": "wood_mdf"
    }
  }
}

// Response
{
  "type": "project.new.response",
  "id": "1",
  "status": "ok",
  "payload": {
    "projectId": "proj_abc123",
    "name": "My Project"
  }
}
```

#### `project.open`
Open an existing project file.

```json
// Request
{
  "type": "project.open",
  "id": "2",
  "payload": {
    "path": "/path/to/project.e3p"
  }
}
```

#### `project.save`
Save the current project.

```json
// Request
{
  "type": "project.save",
  "id": "3",
  "payload": {
    "path": "/path/to/project.e3p"
  }
}
```

#### `project.close`
Close the current project.

### Import

#### `import.stl`
Import an STL file.

```json
// Request
{
  "type": "import.stl",
  "id": "10",
  "payload": {
    "path": "/path/to/model.stl",
    "position": { "x": 0, "y": 0, "z": 0 },
    "scale": 1.0
  }
}

// Response
{
  "type": "import.stl.response",
  "id": "10",
  "status": "ok",
  "payload": {
    "modelId": "model_xyz789",
    "vertices": 12450,
    "triangles": 4150,
    "bounds": {
      "min": { "x": -50, "y": -30, "z": 0 },
      "max": { "x": 50, "y": 30, "z": 25 }
    }
  }
}
```

#### `import.dxf`
Import a DXF file.

```json
// Request
{
  "type": "import.dxf",
  "id": "11",
  "payload": {
    "path": "/path/to/drawing.dxf",
    "layer": "CutPaths"
  }
}
```

#### `import.svg`
Import an SVG file.

```json
// Request
{
  "type": "import.svg",
  "id": "12",
  "payload": {
    "path": "/path/to/design.svg"
  }
}
```

#### `import.step`
Import a STEP file.

```json
// Request
{
  "type": "import.step",
  "id": "13",
  "payload": {
    "path": "/path/to/part.step"
  }
}
```

#### `import.gcode`
Import G-Code for backplot visualization.

```json
// Request
{
  "type": "import.gcode",
  "id": "14",
  "payload": {
    "path": "/path/to/program.gcode"
  }
}
```

### Toolpath

#### `toolpath.generate`
Generate a toolpath.

```json
// Request
{
  "type": "toolpath.generate",
  "id": "20",
  "payload": {
    "operation": "profile",
    "geometryIds": ["geom_001", "geom_002"],
    "tool": {
      "type": "endmill",
      "diameter": 3.175,
      "flutes": 2,
      "fluteLength": 15
    },
    "parameters": {
      "cutDepth": 3,
      "stepDown": 1.5,
      "feedRate": 800,
      "spindleSpeed": 18000,
      "tabs": {
        "enabled": true,
        "width": 5,
        "height": 1,
        "count": 4
      },
      "leadIn": {
        "type": "arc",
        "radius": 3
      },
      "leadOut": {
        "type": "arc",
        "radius": 3
      }
    }
  }
}

// Progress updates (streamed)
{
  "type": "toolpath.progress",
  "payload": {
    "toolpathId": "tp_001",
    "progress": 45,
    "stage": "offsetting"
  }
}

// Response
{
  "type": "toolpath.generate.response",
  "id": "20",
  "status": "ok",
  "payload": {
    "toolpathId": "tp_001",
    "operation": "profile",
    "moves": 245,
    "estimatedTime": 312,
    "bounds": { ... }
  }
}
```

#### `toolpath.delete`
Delete a toolpath.

```json
// Request
{
  "type": "toolpath.delete",
  "id": "21",
  "payload": {
    "toolpathId": "tp_001"
  }
}
```

#### `toolpath.optimize`
Optimize toolpath travel order.

```json
// Request
{
  "type": "toolpath.optimize",
  "id": "22",
  "payload": {
    "toolpathIds": ["tp_001", "tp_002", "tp_003"]
  }
}
```

### Simulation

#### `simulation.start`
Start toolpath simulation.

```json
// Request
{
  "type": "simulation.start",
  "id": "30",
  "payload": {
    "toolpathId": "tp_001",
    "speed": 1.0
  }
}
```

#### `simulation.pause`
Pause simulation.

```json
{
  "type": "simulation.pause",
  "id": "31"
}
```

#### `simulation.resume`
Resume simulation.

```json
{
  "type": "simulation.resume",
  "id": "32"
}
```

#### `simulation.stop`
Stop and reset simulation.

```json
{
  "type": "simulation.stop",
  "id": "33"
}
```

#### `simulation.step`
Step one frame forward/backward.

```json
{
  "type": "simulation.step",
  "id": "34",
  "payload": {
    "direction": "forward",
    "frames": 1
  }
}
```

#### `simulation.speed`
Change simulation speed.

```json
{
  "type": "simulation.speed",
  "id": "35",
  "payload": {
    "speed": 2.5
  }
}
```

#### Simulation Events (Server → Client)

```json
{
  "type": "simulation.update",
  "payload": {
    "frame": 150,
    "totalFrames": 1000,
    "position": { "x": 50, "y": 30, "z": -3 },
    "moveType": "cut",
    "stockRemoval": 0.45
  }
}
```

### Export

#### `export.gcode`
Export toolpaths as G-Code.

```json
// Request
{
  "type": "export.gcode",
  "id": "40",
  "payload": {
    "toolpathIds": ["tp_001", "tp_002"],
    "postProcessor": "grbl",
    "options": {
      "lineNumbers": false,
      "coordinateSystem": "G54",
      "units": "metric",
      "absolute": true
    },
    "outputPath": "/path/to/output.gcode"
  }
}

// Response
{
  "type": "export.gcode.response",
  "id": "40",
  "status": "ok",
  "payload": {
    "path": "/path/to/output.gcode",
    "lines": 1250,
    "estimatedTime": 624,
    "size": 45600
  }
}
```

### Tools

#### `tools.list`
List available tools.

```json
// Request
{
  "type": "tools.list",
  "id": "50"
}

// Response
{
  "type": "tools.list.response",
  "id": "50",
  "status": "ok",
  "payload": {
    "tools": [
      {
        "id": "tool_001",
        "name": "3mm End Mill",
        "type": "endmill",
        "diameter": 3.0,
        "flutes": 2,
        "fluteLength": 15,
        "totalLength": 40
      }
    ]
  }
}
```

#### `tools.add`
Add a new tool.

#### `tools.update`
Update tool parameters.

#### `tools.delete`
Delete a tool.

### Materials

#### `materials.list`
List available materials.

#### `materials.calculate`
Calculate feed/speed for a material and tool combination.

```json
// Request
{
  "type": "materials.calculate",
  "id": "60",
  "payload": {
    "materialId": "mat_aluminum_6061",
    "toolId": "tool_001",
    "operation": "profile"
  }
}

// Response
{
  "type": "materials.calculate.response",
  "id": "60",
  "status": "ok",
  "payload": {
    "feedRate": 500,
    "plungeRate": 100,
    "spindleSpeed": 15000,
    "stepDown": 1.0,
    "stepOver": 1.5
  }
}
```

### Machine

#### `machine.connect`
Connect to a CNC machine via serial.

```json
{
  "type": "machine.connect",
  "id": "70",
  "payload": {
    "port": "/dev/ttyUSB0",
    "baudRate": 115200,
    "controller": "grbl"
  }
}
```

#### `machine.disconnect`
Disconnect from the machine.

#### `machine.status`
Get machine status.

#### `machine.send`
Send G-Code to the machine.

#### `machine.home`
Home the machine.

#### `machine.emergency_stop`
Emergency stop.

## Error Codes

| Code | Description |
|------|-------------|
| `INVALID_REQUEST` | Malformed request message |
| `PROJECT_NOT_OPEN` | No project is currently open |
| `IMPORT_FAILED` | File import failed |
| `INVALID_GEOMETRY` | Geometry is invalid for the operation |
| `TOOLPATH_FAILED` | Toolpath generation failed |
| `SIMULATION_ERROR` | Simulation error |
| `EXPORT_FAILED` | G-Code export failed |
| `MACHINE_NOT_CONNECTED` | Machine is not connected |
| `MACHINE_BUSY` | Machine is busy with another operation |
| `INTERNAL_ERROR` | Internal server error |
