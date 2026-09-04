FROM python:3.12-slim AS runtime
WORKDIR /app

# opencv-python-headless still needs a couple of shared libs on slim Debian images.
# libgl1 specifically provides libGL.so.1 — the others (glib/SM/Xext/Xrender) don't,
# so cv2 import still fails without it even though they're all installed.
RUN apt-get update && apt-get install -y --no-install-recommends \
    libglib2.0-0 libsm6 libxext6 libxrender1 libgl1 \
    && rm -rf /var/lib/apt/lists/* \
    && useradd --create-home --uid 1000 appuser

COPY requirements.txt .
RUN pip install --no-cache-dir --only-binary=:all: -r requirements.txt

COPY main.py geometry.py batchformer_v2.py ./

USER appuser

EXPOSE 8000
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000"]
