export interface ArcFaceModelInfo {
  modelName: string;
  modelVersion: string;
  executionProvider: string;
  sha256: string;
  loadedAtUtc: string;
  vectorSize: number;
  inputImageSize: number;
}

export interface EmbeddingDiagnosticsResponse {
  modelInfo: ArcFaceModelInfo;
  modelPath: string;
  modelFileExists: boolean;
  activeUsers: number;
  totalUsers: number;
  lastUserChangeUtc?: string | null;
}

export interface VectorDocumentStatus {
  userId: string;
  userEmail: string;
  displayName: string;
  vectorDocumentId: string;
  vectorExists: boolean;
  isDeleted: boolean;
  vectorUpdatedAt?: string | null;
  vectorDeletedAt?: string | null;
}

export interface VectorStoreIssue {
  code: string;
  message: string;
}

export interface VectorStoreStatusResponse {
  endpoint: string;
  collectionName: string;
  vectorSize: number;
  autoCreateCollection: boolean;
  isReachable: boolean;
  documents: VectorDocumentStatus[];
  issues: VectorStoreIssue[];
}
