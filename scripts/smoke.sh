#!/usr/bin/env bash
# Synerixis API smoke + demo closed-loop checks
# Usage:
#   ./scripts/smoke.sh
#   BASE_URL=http://127.0.0.1:7092 ./scripts/smoke.sh
#   SKIP_SEED=1 TOKEN=eyJ... ./scripts/smoke.sh
set -euo pipefail

BASE_URL="${BASE_URL:-http://127.0.0.1:7092}"
BASE_URL="${BASE_URL%/}"
TOKEN="${TOKEN:-${SMOKE_TOKEN:-}}"
SKIP_SEED="${SKIP_SEED:-0}"

red() { printf '\033[31m%s\033[0m\n' "$*"; }
green() { printf '\033[32m%s\033[0m\n' "$*"; }
yellow() { printf '\033[33m%s\033[0m\n' "$*"; }

fail=0
BODY=/tmp/synerixis_smoke_body.txt

check_get() {
  local path="$1"
  local label="$2"
  local code
  code=$(curl -sS -o "$BODY" -w '%{http_code}' "${BASE_URL}${path}" || echo "000")
  if [[ "$code" == "200" ]]; then
    green "OK  ${label}  GET ${path} → ${code}"
    head -c 220 "$BODY"; echo
  else
    red "FAIL ${label}  GET ${path} → ${code}"
    head -c 400 "$BODY"; echo
    fail=1
  fi
}

check_auth_get() {
  local path="$1"
  local label="$2"
  local code
  code=$(curl -sS -o "$BODY" -w '%{http_code}' \
    -H "Authorization: Bearer ${TOKEN}" \
    "${BASE_URL}${path}" || echo "000")
  if [[ "$code" == "200" ]]; then
    green "OK  ${label}  GET ${path} → ${code}"
    head -c 220 "$BODY"; echo
  else
    red "FAIL ${label}  GET ${path} → ${code}"
    head -c 400 "$BODY"; echo
    fail=1
  fi
}

json_field() {
  # minimal extract: "key":"value" or "key": guid
  python3 - "$1" "$2" <<'PY'
import json,sys
key=sys.argv[2]
try:
  data=json.load(open(sys.argv[1]))
except Exception as e:
  print("", end=""); sys.exit(0)
v=data
for part in key.split("."):
  if isinstance(v, dict) and part in v:
    v=v[part]
  else:
    print("", end=""); sys.exit(0)
print(v if v is not None else "", end="")
PY
}

echo "=== Synerixis smoke @ ${BASE_URL} ==="
check_get "/health" "liveness"
check_get "/health/ready" "readiness (DB)"

if [[ "$SKIP_SEED" != "1" ]]; then
  code=$(curl -sS -o "$BODY" -w '%{http_code}' -X POST "${BASE_URL}/api/dev/seed-demo" || echo "000")
  if [[ "$code" == "200" ]]; then
    green "OK  seed-demo  POST /api/dev/seed-demo → ${code}"
    head -c 280 "$BODY"; echo
  else
    yellow "WARN seed-demo → ${code} (Development only; continuing)"
    head -c 200 "$BODY"; echo
  fi
fi

# Prefer Admin JWT via init-agent (must be Role=Admin)
if [[ -z "${TOKEN}" ]]; then
  code=$(curl -sS -o "$BODY" -w '%{http_code}' -X POST "${BASE_URL}/api/auth/init-agent" || echo "000")
  if [[ "$code" == "200" ]]; then
    TOKEN=$(json_field "$BODY" "token")
    ROLE=$(json_field "$BODY" "role")
    green "OK  init-agent → Admin JWT (role=${ROLE:-?})"
    if [[ -n "${ROLE}" && "${ROLE}" != "Admin" ]]; then
      red "FAIL init-agent returned non-Admin role=${ROLE}"
      fail=1
    fi
  else
    yellow "WARN init-agent → ${code}; try agent-login admin@test.com"
  fi
fi

# Merchant phone login (demo)
MERCHANT_TOKEN=""
code=$(curl -sS -o "$BODY" -w '%{http_code}' -X POST "${BASE_URL}/api/auth/phone-login" \
  -H 'Content-Type: application/json' \
  -d '{"phone":"13800138000","code":"123456","countryCode":"86"}' || echo "000")
if [[ "$code" == "200" ]]; then
  MERCHANT_TOKEN=$(json_field "$BODY" "token")
  green "OK  phone-login demo merchant → ${code}"
else
  yellow "WARN phone-login → ${code} (run seed-demo first)"
  head -c 200 "$BODY"; echo
fi

USE_TOKEN="${MERCHANT_TOKEN:-$TOKEN}"
if [[ -n "${USE_TOKEN}" ]]; then
  code=$(curl -sS -o "$BODY" -w '%{http_code}' \
    -H "Authorization: Bearer ${USE_TOKEN}" \
    "${BASE_URL}/api/merchant/sessions?take=10" || echo "000")
  if [[ "$code" == "200" ]]; then
    green "OK  merchant sessions → ${code}"
    head -c 260 "$BODY"; echo
    # try approve first pending draft if any
    SESSION_ID=$(python3 - "$BODY" <<'PY'
import json,sys
d=json.load(open(sys.argv[1]))
items=d.get("items") or d.get("data") or []
if isinstance(items, dict) and "$values" in items:
  items = items["$values"]
if not isinstance(items, list):
  items = []
for it in items:
  if isinstance(it, dict) and it.get("hasPendingDraft"):
    print(it.get("id") or "", end=""); break
PY
)
    if [[ -n "${SESSION_ID}" ]]; then
      code=$(curl -sS -o "$BODY" -w '%{http_code}' -X POST \
        -H "Authorization: Bearer ${USE_TOKEN}" \
        "${BASE_URL}/api/merchant/sessions/${SESSION_ID}/draft/approve" || echo "000")
      if [[ "$code" == "200" ]]; then
        green "OK  draft approve (mock/sim) session=${SESSION_ID} → ${code}"
        head -c 220 "$BODY"; echo
      else
        red "FAIL draft approve → ${code}"
        head -c 400 "$BODY"; echo
        fail=1
      fi
    else
      yellow "SKIP draft approve — no pending draft in list"
    fi
  else
    red "FAIL merchant sessions → ${code}"
    head -c 400 "$BODY"; echo
    fail=1
  fi
else
  yellow "SKIP sessions — no TOKEN. Set TOKEN=... or ensure Development + seed-demo"
fi

# Admin dashboard if we have Admin token
if [[ -n "${TOKEN}" ]]; then
  code=$(curl -sS -o "$BODY" -w '%{http_code}' \
    -H "Authorization: Bearer ${TOKEN}" \
    "${BASE_URL}/api/admin/dashboard" || echo "000")
  if [[ "$code" == "200" ]]; then
    green "OK  admin dashboard → ${code}"
    head -c 200 "$BODY"; echo
  elif [[ "$code" == "401" || "$code" == "403" ]]; then
    red "FAIL admin dashboard auth → ${code} (init-agent must return Admin role)"
    head -c 300 "$BODY"; echo
    fail=1
  else
    yellow "WARN admin dashboard → ${code}"
    head -c 200 "$BODY"; echo
  fi
fi

if [[ "$fail" -ne 0 ]]; then
  red "Smoke FAILED"
  exit 1
fi
green "Smoke PASSED"
echo "Demo accounts: merchant 13800138000/123456 · agent agent@demo.synerixis.local/Agent123! · admin admin@test.com/Agent123!"
