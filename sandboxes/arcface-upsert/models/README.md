# ArcFace Model Placeholder

Drop the production ArcFace ONNX checkpoint (for example `arcface_r100_v1.onnx`, ~249 MB) in this folder.

The embedding service defaults to `%APP_CONTEXT%/models/arcface_r100_v1.onnx`, so placing the file here aligns with the configuration in `appsettings*.json`. Add the matching SHA256 text file if you plan to enable checksum verification.
