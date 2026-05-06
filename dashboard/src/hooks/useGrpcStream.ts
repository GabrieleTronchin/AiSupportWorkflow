import { useGrpcStreamContext } from './GrpcStreamContext';

/**
 * Hook that returns the shared gRPC stream state.
 * The stream is managed at the app root level (GrpcStreamProvider)
 * so it persists across page navigations.
 */
export function useGrpcStream() {
  return useGrpcStreamContext();
}
