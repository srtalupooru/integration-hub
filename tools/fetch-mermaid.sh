#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
version=11.12.0
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
curl --fail --silent --show-error --location "https://registry.npmjs.org/mermaid/-/mermaid-${version}.tgz" -o "$tmp/mermaid.tgz"
tar -xzf "$tmp/mermaid.tgz" -C "$tmp"
python3 tools/vendor-mermaid.py "$tmp/package" "$tmp/vendor"
rm -rf src/IntegrationHub.Web/wwwroot/vendor/mermaid
mkdir -p src/IntegrationHub.Web/wwwroot/vendor
mv "$tmp/vendor" src/IntegrationHub.Web/wwwroot/vendor/mermaid
