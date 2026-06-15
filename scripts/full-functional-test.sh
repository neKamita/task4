#!/usr/bin/env bash
set -euo pipefail

suffix="$(date +%s)"
network="task4-e2e-$suffix"
db="task4-e2e-db-$suffix"
app="task4-e2e-app-$suffix"
tmpdir="$(mktemp -d)"
image="task4-app:latest"
sdk="mcr.microsoft.com/dotnet/sdk:8.0"
pg="postgres:16-alpine"

cleanup() {
    docker stop "$app" "$db" >/dev/null 2>&1 || true
    docker network rm "$network" >/dev/null 2>&1 || true
    rm -rf "$tmpdir"
}

trap cleanup EXIT

docker network create "$network" >/dev/null

docker run -d --rm \
    --name "$db" \
    --network "$network" \
    -e POSTGRES_DB=task4 \
    -e POSTGRES_USER=task4 \
    -e POSTGRES_PASSWORD=task4 \
    "$pg" >/dev/null

docker run -d --rm \
    --name "$app" \
    --network "$network" \
    -e "ConnectionStrings__Default=Host=$db;Port=5432;Database=task4;Username=task4;Password=task4" \
    -e "App__BaseUrl=http://$app:8080" \
    "$image" >/dev/null

docker run --rm --network "$network" "$sdk" bash -lc "
for i in {1..60}; do
    curl -fsS http://$app:8080/Account/Login >/dev/null 2>&1 && exit 0
    sleep 1
done
exit 1
"

run_http() {
    docker run --rm \
        --network "$network" \
        -e "BASE_URL=http://$app:8080" \
        -e "TMP_DIR=/tmp/e2e" \
        -e "RUN_ID=$suffix" \
        -v "$tmpdir:/tmp/e2e" \
        "$sdk" bash -lc "$1"
}

psql_value() {
    docker run --rm \
        --network "$network" \
        -e PGPASSWORD=task4 \
        "$pg" psql -h "$db" -U task4 -d task4 -At -c "$1"
}

psql_table() {
    docker run --rm \
        --network "$network" \
        -e PGPASSWORD=task4 \
        "$pg" psql -h "$db" -U task4 -d task4 -c "$1"
}

require_eq() {
    local actual="$1"
    local expected="$2"
    local title="$3"

    if [[ "$actual" != "$expected" ]]; then
        echo "FAIL $title: expected '$expected', got '$actual'"
        exit 1
    fi

    echo "PASS $title"
}

http_script='
set -euo pipefail

get_token() {
    local file="$1"
    local line
    line="$(grep -m1 "__RequestVerificationToken" "$file")"
    line="${line#*value=\"}"
    echo "${line%%\"*}"
}

register_user() {
    local jar="$1"
    local name="$2"
    local email="$3"
    local password="$4"
    local out="$5"

    curl -s -c "$jar" "$BASE_URL/Account/Register" -o "$TMP_DIR/register.html"
    local token
    token="$(get_token "$TMP_DIR/register.html")"
    curl -s -b "$jar" -c "$jar" -o "$TMP_DIR/$out.html" -w "%{http_code} %{redirect_url}" \
        -X POST "$BASE_URL/Account/Register" \
        --data-urlencode "__RequestVerificationToken=$token" \
        --data-urlencode "Name=$name" \
        --data-urlencode "Email=$email" \
        --data-urlencode "Password=$password"
}

login_user() {
    local jar="$1"
    local email="$2"
    local password="$3"
    local out="$4"

    curl -s -b "$jar" -c "$jar" "$BASE_URL/Account/Login" -o "$TMP_DIR/login.html"
    local token
    token="$(get_token "$TMP_DIR/login.html")"
    curl -s -b "$jar" -c "$jar" -o "$TMP_DIR/$out.html" -w "%{http_code} %{redirect_url}" \
        -X POST "$BASE_URL/Account/Login" \
        --data-urlencode "__RequestVerificationToken=$token" \
        --data-urlencode "Email=$email" \
        --data-urlencode "Password=$password"
}

post_users() {
    local jar="$1"
    local action="$2"
    shift 2

    curl -s -b "$jar" -c "$jar" "$BASE_URL/Users" -o "$TMP_DIR/users.html"
    local token
    token="$(get_token "$TMP_DIR/users.html")"
    local args=(curl -s -b "$jar" -c "$jar" -o "$TMP_DIR/$action.html" -w "%{http_code} %{redirect_url}" -X POST "$BASE_URL/Users/$action" --data-urlencode "__RequestVerificationToken=$token")

    for id in "$@"; do
        args+=(--data-urlencode "selectedIds=$id")
    done

    "${args[@]}"
}

admin_email="admin-$RUN_ID@example.com"
other_email="other-$RUN_ID@example.com"
unverified_email="unverified-$RUN_ID@example.com"
self_email="self-$RUN_ID@example.com"
all1_email="all1-$RUN_ID@example.com"
all2_email="all2-$RUN_ID@example.com"

echo "register_admin=$(register_user "$TMP_DIR/admin.jar" Admin "$admin_email" x register-admin)"
echo "register_other=$(register_user "$TMP_DIR/other.jar" Other "$other_email" x register-other)"
echo "register_unverified=$(register_user "$TMP_DIR/unverified.jar" Unverified "$unverified_email" x register-unverified)"

curl -s -c "$TMP_DIR/dupe.jar" "$BASE_URL/Account/Register" -o "$TMP_DIR/dupe-form.html"
dupe_token="$(get_token "$TMP_DIR/dupe-form.html")"
dupe_status="$(curl -s -b "$TMP_DIR/dupe.jar" -c "$TMP_DIR/dupe.jar" -o "$TMP_DIR/dupe.html" -w "%{http_code}" \
    -X POST "$BASE_URL/Account/Register" \
    --data-urlencode "__RequestVerificationToken=$dupe_token" \
    --data-urlencode "Name=Dupe" \
    --data-urlencode "Email=${admin_email^^}" \
    --data-urlencode "Password=x")"
dupe_text="$(grep -o "This email is already registered" "$TMP_DIR/dupe.html" || true)"
echo "duplicate=$dupe_status $dupe_text"

echo "login_admin=$(login_user "$TMP_DIR/admin.jar" "$admin_email" x login-admin)"
echo "users_after_login=$(curl -s -b "$TMP_DIR/admin.jar" -o "$TMP_DIR/users-after-login.html" -w "%{http_code}" "$BASE_URL/Users")"
'

RUN_ID="$suffix" run_http "$http_script" | tee "$tmpdir/http-1.log"

admin_email="admin-$suffix@example.com"
other_email="other-$suffix@example.com"
unverified_email="unverified-$suffix@example.com"
self_email="self-$suffix@example.com"
all1_email="all1-$suffix@example.com"
all2_email="all2-$suffix@example.com"

admin_id="$(psql_value "SELECT id FROM users WHERE email='$admin_email';")"
other_id="$(psql_value "SELECT id FROM users WHERE email='$other_email';")"
unverified_id="$(psql_value "SELECT id FROM users WHERE email='$unverified_email';")"
admin_token="$(psql_value "SELECT confirmation_token FROM users WHERE email='$admin_email';")"
other_token="$(psql_value "SELECT confirmation_token FROM users WHERE email='$other_email';")"

require_eq "$(grep -c "register_admin=302" "$tmpdir/http-1.log")" "1" "registration"
require_eq "$(grep -c "duplicate=200 This email is already registered" "$tmpdir/http-1.log")" "1" "duplicate email message"
require_eq "$(grep -c "login_admin=302" "$tmpdir/http-1.log")" "1" "login"
require_eq "$(grep -c "users_after_login=200" "$tmpdir/http-1.log")" "1" "users page"

run_http "curl -s -o /tmp/e2e/confirm-admin.html -w '%{http_code} %{redirect_url}' \"\$BASE_URL/Account/Confirm?token=$admin_token\"" > "$tmpdir/confirm-admin.log"
run_http "curl -s -o /tmp/e2e/confirm-other.html -w '%{http_code} %{redirect_url}' \"\$BASE_URL/Account/Confirm?token=$other_token\"" > "$tmpdir/confirm-other.log"

require_eq "$(psql_value "SELECT status FROM users WHERE id=$admin_id;")" "Active" "email confirmation admin"
require_eq "$(psql_value "SELECT status FROM users WHERE id=$other_id;")" "Active" "email confirmation other"

action_script='
set -euo pipefail
source /tmp/e2e/http-functions.sh
'

cat > "$tmpdir/http-functions.sh" <<'EOS'
get_token() {
    local file="$1"
    local line
    line="$(grep -m1 "__RequestVerificationToken" "$file")"
    line="${line#*value=\"}"
    echo "${line%%\"*}"
}

post_users() {
    local jar="$1"
    local action="$2"
    shift 2

    curl -s -b "$jar" -c "$jar" "$BASE_URL/Users" -o "$TMP_DIR/users.html"
    local token
    token="$(get_token "$TMP_DIR/users.html")"
    local args=(curl -s -b "$jar" -c "$jar" -o "$TMP_DIR/$action.html" -w "%{http_code} %{redirect_url}" -X POST "$BASE_URL/Users/$action" --data-urlencode "__RequestVerificationToken=$token")

    for id in "$@"; do
        args+=(--data-urlencode "selectedIds=$id")
    done

    "${args[@]}"
}

register_user() {
    local jar="$1"
    local name="$2"
    local email="$3"
    local password="$4"
    local out="$5"

    curl -s -c "$jar" "$BASE_URL/Account/Register" -o "$TMP_DIR/register.html"
    local token
    token="$(get_token "$TMP_DIR/register.html")"
    curl -s -b "$jar" -c "$jar" -o "$TMP_DIR/$out.html" -w "%{http_code} %{redirect_url}" \
        -X POST "$BASE_URL/Account/Register" \
        --data-urlencode "__RequestVerificationToken=$token" \
        --data-urlencode "Name=$name" \
        --data-urlencode "Email=$email" \
        --data-urlencode "Password=$password"
}

login_user() {
    local jar="$1"
    local email="$2"
    local password="$3"
    local out="$4"

    curl -s -b "$jar" -c "$jar" "$BASE_URL/Account/Login" -o "$TMP_DIR/login.html"
    local token
    token="$(get_token "$TMP_DIR/login.html")"
    curl -s -b "$jar" -c "$jar" -o "$TMP_DIR/$out.html" -w "%{http_code} %{redirect_url}" \
        -X POST "$BASE_URL/Account/Login" \
        --data-urlencode "__RequestVerificationToken=$token" \
        --data-urlencode "Email=$email" \
        --data-urlencode "Password=$password"
}
EOS

run_http "source /tmp/e2e/http-functions.sh; echo block_other=\$(post_users /tmp/e2e/admin.jar Block $other_id)" > "$tmpdir/block.log"
require_eq "$(psql_value "SELECT status FROM users WHERE id=$other_id;")" "Blocked" "non-current user blocking"

run_http "source /tmp/e2e/http-functions.sh; echo blocked_login=\$(login_user /tmp/e2e/blocked.jar '$other_email' x blocked-login)" > "$tmpdir/blocked-login.log"
require_eq "$(grep -c "blocked_login=200" "$tmpdir/blocked-login.log")" "1" "blocked user cannot login"

run_http "source /tmp/e2e/http-functions.sh; echo unblock_other=\$(post_users /tmp/e2e/admin.jar Unblock $other_id)" > "$tmpdir/unblock.log"
require_eq "$(psql_value "SELECT status FROM users WHERE id=$other_id;")" "Active" "unblocking"

run_http "source /tmp/e2e/http-functions.sh; echo delete_other=\$(post_users /tmp/e2e/admin.jar Delete $other_id)" > "$tmpdir/delete-other.log"
require_eq "$(psql_value "SELECT COUNT(*) FROM users WHERE id=$other_id;")" "0" "delete non-current user"

run_http "source /tmp/e2e/http-functions.sh; echo register_deleted_again=\$(register_user /tmp/e2e/other2.jar Other2 '$other_email' x register-other2)" > "$tmpdir/register-deleted.log"
require_eq "$(grep -c "register_deleted_again=302" "$tmpdir/register-deleted.log")" "1" "deleted user can re-register"

run_http "source /tmp/e2e/http-functions.sh; echo delete_unverified=\$(post_users /tmp/e2e/admin.jar DeleteUnverified)" > "$tmpdir/delete-unverified.log"
require_eq "$(psql_value "SELECT COUNT(*) FROM users WHERE email IN ('$unverified_email', '$other_email');")" "0" "delete unverified"

run_http "source /tmp/e2e/http-functions.sh; echo register_self=\$(register_user /tmp/e2e/self.jar Self '$self_email' x register-self); echo login_self=\$(login_user /tmp/e2e/self.jar '$self_email' x login-self)" > "$tmpdir/self-register.log"
self_id="$(psql_value "SELECT id FROM users WHERE email='$self_email';")"
run_http "source /tmp/e2e/http-functions.sh; echo delete_self=\$(post_users /tmp/e2e/self.jar Delete $self_id)" > "$tmpdir/self-delete.log"
require_eq "$(grep -c "delete_self=302" "$tmpdir/self-delete.log")" "1" "self delete redirects"
require_eq "$(psql_value "SELECT COUNT(*) FROM users WHERE id=$self_id;")" "0" "self delete removes user"

run_http "source /tmp/e2e/http-functions.sh; echo register_all1=\$(register_user /tmp/e2e/all1.jar All1 '$all1_email' x register-all1); echo register_all2=\$(register_user /tmp/e2e/all2.jar All2 '$all2_email' x register-all2); echo login_all1=\$(login_user /tmp/e2e/all1.jar '$all1_email' x login-all1)" > "$tmpdir/all-register.log"
all1_id="$(psql_value "SELECT id FROM users WHERE email='$all1_email';")"
all2_id="$(psql_value "SELECT id FROM users WHERE email='$all2_email';")"
run_http "source /tmp/e2e/http-functions.sh; echo block_all=\$(post_users /tmp/e2e/all1.jar Block $admin_id $all1_id $all2_id)" > "$tmpdir/all-block.log"

require_eq "$(grep -c "block_all=302" "$tmpdir/all-block.log")" "1" "all users block redirects current"
require_eq "$(psql_value "SELECT COUNT(*) FROM users WHERE id IN ($admin_id, $all1_id, $all2_id) AND status='Blocked';")" "3" "all selected users blocked"

psql_table "SELECT indexname, indexdef FROM pg_indexes WHERE tablename='users' ORDER BY indexname;"

echo "PASS full functional test"
