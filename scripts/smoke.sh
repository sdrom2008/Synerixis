#!/usr/bin/env bash
# Synerixis API smoke checks
# Usage:
#   ./scripts/smoke.sh
#   BASE_URL=http://localhost:5000 TOKEN=eyJ... ./scripts/smoke.sh
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:5000}"
BASE_URL="${BASE_URL%/}"
TOKEN="${TOKEN:-${SMOKE_TOKEN:-}}"

red() { printf '\033[31m%s\033[0m\n' "$*"; }
green() { printf '\033[32m%s\033[0m\n' "$*"; }
yellow() { printf '\033[33m%s\033[0m\n' "$*"; }

fail=0

check_get() {
  local path="$1"
  local label="$2"
  local code
  code=$(curl -sS -o /tmp/synerixis_smoke_body.txt -w '%{http_code}' "${BASE_URL}${path}" || echo "000")
  if [[ "$code" == "200" ]]; then
    green "OK  ${label}  GET ${path} → ${code}"
    head -c 200 /tmp/synerixis_smoke_body.txt; echo
  else
    red "FAIL ${label}  GET ${path} → ${code}"
    head -c 400 /tmp/synerixis_smoke_body.txt; echo
    fail=1
  fi
}

check_auth_get() {
  local path="$1"
  local label="$2"
  local code
  code=$(curl -sS -o /tmp/synerixis_smoke_body.txt -w '%{http_code}' \
    -H "Authorization: Bearer ${TOKEN}" \
    "${BASE_URL}${path}" || echo "000")
  if [[ "$code" == "200" ]]; then
    green "OK  ${label}  GET ${path} → ${code}"
    head -c 200 /tmp/synerixis_smoke_body.txt; echo
  else
    red "FAIL ${label}  GET ${path} → ${code}"
    head -c 400 /tmp/synerixis_smoke_body.txt; echo
    fail=1
  fi
}

echo "=== Synerixis smoke @ ${BASE_URL} ==="
check_get "/health" "liveness"
check_get "/health/ready" "readiness (DB)"

if [[ -z "${TOKEN}" ]]; then
  yellow "SKIP sessions — no TOKEN. Set TOKEN=... or SMOKE_TOKEN=..."
  yellow "开发测号提示："
  yellow "  curl -sS -X POST ${BASE_URL}/api/auth/init-agent"
  yellow "  （仅 ASPNETCORE_ENVIRONMENT=Development；返回 JWT，可用作 TOKEN）"
  yellow "  或坐席登录：admin@test.com / Agent123!（init-agent 创建后）"
else
  check_auth_get "/api/merchant/sessions?take=5" "sessions (auth)"
fi

if [[ "$fail" -ne 0 ]]; then
  red "Smoke FAILED"
  exit 1
fi
green "Smoke PASSED"
