FROM mcr.microsoft.com/devcontainers/base:jammy

ARG EXTENSION_ID="streetsidesoftware.code-spell-checker"
ENV EXTENSION_ID=${EXTENSION_ID}
ENV EXTENSION_CACHE=/opt/extensions/${EXTENSION_ID}

RUN apt-get update \
    && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
        python3 \
        ca-certificates \
        curl \
    && rm -rf /var/lib/apt/lists/*

COPY fetch_extension.py /usr/local/bin/fetch_extension.py

RUN mkdir -p ${EXTENSION_CACHE} \
    && EXTENSION_ID=${EXTENSION_ID} EXTENSION_CACHE=${EXTENSION_CACHE} \
        python3 /usr/local/bin/fetch_extension.py

VOLUME ["/opt/extensions/streetsidesoftware.code-spell-checker"]

CMD ["sleep", "infinity"]
