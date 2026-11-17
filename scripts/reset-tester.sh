#!/usr/bin/env bash
# Reset a tester number on the Dev 1HMS API so it can sign up again (docs/TESTER-ACCOUNTS.md).
#
#   TESTER_RESET_KEY=...  scripts/reset-tester.sh 9000000001            # shows what would go, then asks
#   TESTER_RESET_KEY=...  scripts/reset-tester.sh 9000000001 --yes      # no question (for scripts)
#   TESTER_RESET_KEY=...  scripts/reset-tester.sh 9000000001 --dry-run  # only show
#
# API_URL defaults to the Dev API. The route does not exist on production (404).
set -euo pipefail

MOBILE="${1:?usage: reset-tester.sh <10-digit tester mobile> [--dry-run|--yes]}"
MODE="${2:-}"
API_URL="${API_URL:-https://1hms-dev-api.nexeagle.com}"
: "${TESTER_RESET_KEY:?set TESTER_RESET_KEY (the DEV_TESTER_RESET_KEY secret)}"

call() {
  curl -sS -X POST "$API_URL/internal/tester-accounts/reset" \
    -H "Content-Type: application/json" -H "X-Tester-Key: $TESTER_RESET_KEY" \
    -d "{\"mobileNumber\":\"$MOBILE\",\"dryRun\":$1}"
  echo
}

if [ "$MODE" != "--yes" ]; then
  echo "What a reset of $MOBILE would delete on $API_URL:"
  call true
  [ "$MODE" = "--dry-run" ] && exit 0
  read -r -p "Delete this? [y/N] " answer
  [ "$answer" = "y" ] || [ "$answer" = "Y" ] || { echo "Nothing was changed."; exit 0; }
fi
call false
