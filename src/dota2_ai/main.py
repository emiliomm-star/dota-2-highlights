"""Servicio gRPC del detector de highlights de Dota 2.

Fase 1: arranca 'idle' — abre el puerto 50051 y espera. En fases
posteriores se registran aquí los servicios de inferencia (ONNX Runtime).
"""
import os
import signal
from concurrent import futures

import grpc

PORT = os.getenv("GRPC_PORT", "50051")


def serve() -> None:
    server = grpc.server(futures.ThreadPoolExecutor(max_workers=4))
    # TODO (Fase 2): registrar aquí el servicer de detección.
    server.add_insecure_port(f"[::]:{PORT}")
    server.start()
    print(f"[dota2_ai] servidor gRPC escuchando en :{PORT}", flush=True)

    # Apagado limpio ante SIGTERM (docker stop) / Ctrl+C.
    def _shutdown(*_):
        server.stop(grace=5)

    signal.signal(signal.SIGTERM, _shutdown)
    try:
        server.wait_for_termination()
    except KeyboardInterrupt:
        server.stop(grace=5)


if __name__ == "__main__":
    serve()
