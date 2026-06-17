# WebSocket API

The C++ backend exposes a JSON-over-WebSocket API at `ws://localhost:9001`.

This document reflects the dispatch table in `src/api/MessageHandler.cpp`. It intentionally documents the current C++ backend protocol, not the older planned API names.

## Message envelope

Request:

```json
{
  "type": "project.new",
  "id": "client-generated-request-id",
  "payload": {}
}
```

Successful response:

```json
{
  "status": "ok",
  "id": "client-generated-request-id",
  "data": {}
}
```

Error response:

```json
{
  "status": "error",
  "id": "client-generated-request-id",
  "message": "Human readable error"
}
```

Notes:

- Request `id` is copied into the response when provided.
- Successful handlers usually place response content under `data`.
- The response does not currently include a `type` field.
- The frontend resolves pending requests with `data`, then `payload`, then the whole message.

## Implemented message types

### Project

#### `project.new`

Creates a new in-memory project.

```json
{
  "type": "project.new",
  "id": "1",
  "payload": {
    "name": "New Project"
  }
}
```

Response data:

```json
{
  "message": "Proje oluşturuldu"
}
```

#### `project.open`

Loads a project from disk.

```json
{
  "type": "project.open",
  "id": "2",
  "payload": {
    "path": "/absolute/path/project.e3p"
  }
}
```

Errors if the file cannot be opened or parsed.

#### `project.save`

Saves the active project.

```json
{
  "type": "project.save",
  "id": "3",
  "payload": {
    "path": "/absolute/path/project.e3p"
  }
}
```

If `path` is omitted or empty, the backend tries to save back to the last loaded/saved path. That fails for a brand-new unsaved project.

#### `project.get`

Returns the current project JSON.

```json
{
  "type": "project.get",
  "id": "4"
}
```

The project object currently includes IDs, name, timestamps/output directory if present, tool library, and operations. Model references exist in the C++ project structure but are not currently emitted by `Project::toJson()`.

### Models and meshes

#### `model.load`

Registers a model file path in the current project.

```json
{
  "type": "model.load",
  "id": "10",
  "payload": {
    "filePath": "/absolute/path/model.step",
    "role": "workpiece"
  }
}
```

Supported role values:

- `workpiece`
- `stock`
- `fixture`

Response data:

```json
{
  "modelId": "model_123"
}
```

Important current limitation: mesh loading uses `GeometryKernel::loadSTEP` in `mesh.get`, so STEP-like files are the safe documented path for the C++ web backend today. The broader importer classes exist elsewhere in the repository, especially in the WPF/C# app.

#### `mesh.get`

Tessellates a registered model and returns interleaved vertex data for Three.js.

```json
{
  "type": "mesh.get",
  "id": "11",
  "payload": {
    "modelId": "model_123"
  }
}
```

Response data:

```json
{
  "vertexCount": 1000,
  "triangleCount": 333,
  "buffer": [0.0, 0.0, 0.0, 0.0, 0.0, 1.0],
  "bbox": {
    "min": [0.0, 0.0, 0.0],
    "max": [100.0, 50.0, 10.0]
  }
}
```

The buffer is interleaved as repeated `x, y, z, nx, ny, nz` float values.

### Operations

#### `operation.add`

Adds a machining operation to the current project.

```json
{
  "type": "operation.add",
  "id": "20",
  "payload": {
    "name": "Operation 1",
    "type": 0,
    "toolId": "tool_1",
    "geometryRef": "model_123:face_1",
    "feedrateXY": 1200,
    "feedrateZ": 400,
    "spindleSpeed": 8000,
    "depthOfCut": 2.0,
    "stepover": 4.0,
    "stockToLeave": 0.0,
    "tolerance": 0.01
  }
}
```

Response data:

```json
{
  "id": "generated-operation-id"
}
```

Current C++ enum values:

- `0`: Pocket2D
- `1`: Contour2D
- `2`: SurfaceFinishing
- `3`: AdaptiveClearing
- `4`: Drilling
- `5`: Threading

Frontend note: the current React UI sends string operation types such as `Pocket2D`. The C++ deserializer currently reads operation type as an integer with a fallback to `0`, so string values are treated as Pocket2D unless that mapping is expanded.

#### `operation.update`

Replaces an existing operation by ID.

```json
{
  "type": "operation.update",
  "id": "21",
  "payload": {
    "id": "generated-operation-id",
    "name": "Updated operation",
    "type": 0,
    "toolId": "tool_1",
    "feedrateXY": 1000,
    "feedrateZ": 300,
    "spindleSpeed": 9000,
    "depthOfCut": 1.5,
    "stepover": 3.0,
    "stockToLeave": 0.0,
    "tolerance": 0.01
  }
}
```

Errors if the operation ID does not exist.

#### `operation.remove`

Removes an operation.

```json
{
  "type": "operation.remove",
  "id": "22",
  "payload": {
    "id": "generated-operation-id"
  }
}
```

#### `operation.compute`

Starts asynchronous toolpath computation for an operation.

```json
{
  "type": "operation.compute",
  "id": "23",
  "payload": {
    "operationId": "generated-operation-id"
  }
}
```

Immediate response data:

```json
{
  "message": "Hesaplama başlatıldı",
  "operationId": "generated-operation-id"
}
```

The code comments indicate that results are intended to arrive through EventBus/WebSocket push messages. The frontend already listens for `toolpath.generated` events, but event wiring should be verified when extending this flow.

### Toolpaths and export

#### `toolpath.get`

Returns summary information for a computed toolpath.

```json
{
  "type": "toolpath.get",
  "id": "30",
  "payload": {
    "toolpathId": "toolpath-id"
  }
}
```

Response data:

```json
{
  "moveCount": 245,
  "estimatedTime": 312.0,
  "cuttingLength": 1234.5
}
```

#### `toolpath.export`

Exports one computed toolpath to a G-Code file.

```json
{
  "type": "toolpath.export",
  "id": "31",
  "payload": {
    "toolpathId": "toolpath-id",
    "outputPath": "/absolute/path/output.nc",
    "postProcessor": "fanuc"
  }
}
```

Supported C++ post processor IDs in the handler today:

- `fanuc`
- `heidenhain`
- anything else falls back to `generic`

The C++ generator also defines `haas()`, but the current API handler does not select it yet.

Response data:

```json
{
  "path": "/absolute/path/output.nc"
}
```

Errors when:

- no project is open
- the toolpath ID is unknown
- no matching/fallback tool can be found in the project tool library
- the output file cannot be written

### Simulation

#### `simulation.start`

```json
{
  "type": "simulation.start",
  "id": "40",
  "payload": {
    "toolpathId": "toolpath-id"
  }
}
```

Current response data:

```json
{
  "message": "Simülasyon başlatıldı"
}
```

#### `simulation.step`

```json
{
  "type": "simulation.step",
  "id": "41",
  "payload": {
    "direction": "forward",
    "frames": 1
  }
}
```

Current response data:

```json
{
  "message": "Adım ilerledi"
}
```

#### `simulation.pause`

```json
{
  "type": "simulation.pause",
  "id": "42"
}
```

Current response data:

```json
{
  "message": "Simülasyon durduruldu"
}
```

The React client also listens for `simulation.frame` push events.

### AI feed/speed prediction

#### `ai.optimize`

Runs the current physics-based cutting-parameter fallback.

```json
{
  "type": "ai.optimize",
  "id": "50",
  "payload": {
    "hardnessHRC": 30,
    "toolDiameter": 10,
    "toolType": 0,
    "toolMaterial": 0,
    "axialDepth": 3,
    "radialStepover": 4,
    "operationType": 0,
    "targetRoughness": 1.6
  }
}
```

Response data:

```json
{
  "feedrate": 1234.0,
  "spindleSpeed": 5678.0,
  "predictedRoughness": 1.92,
  "toolLifeMinutes": 30.0,
  "confidence": 0.5
}
```

The constructor accepts a model path, but ONNX inference is disabled in the current code. The implementation logs that physics mode is active and returns deterministic fallback values.

## Push events expected by the React client

The React client in `ui/src/lib/wsClient.ts` handles these server-to-client event names:

- `system.ready`
- `toolpath.generated`
- `simulation.frame`
- `ai.prediction`
- `error`

When adding backend functionality, keep these event names stable or update the frontend in the same change.

## Removed or outdated planned names

The older docs mentioned these names, but they are not in the current C++ dispatch table:

- `import.stl`, `import.dxf`, `import.svg`, `import.step`, `import.gcode`
- `toolpath.generate`, `toolpath.delete`, `toolpath.optimize`
- `export.gcode`
- `tools.list`, `tools.add`, `tools.update`, `tools.delete`
- `materials.list`, `materials.calculate`
- `machine.connect`, `machine.disconnect`, `machine.status`, `machine.send`, `machine.home`, `machine.emergency_stop`

Those may be useful future API names, but they should not be documented as current backend behavior until implemented.
