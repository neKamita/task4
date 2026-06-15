#!/usr/bin/env bash
set -euo pipefail

base_url="${BASE_URL:-http://app:8080}"
email="alice$(date +%s)@example.com"

get_token() {
    local file="$1"
    local line
    line="$(grep -m1 "__RequestVerificationToken" "$file")"
    line="${line#*value=\"}"
    echo "${line%%\"*}"
}

curl -s -c /tmp/cookies "$base_url/Account/Register" -o /tmp/register.html
token="$(get_token /tmp/register.html)"

register_status="$(curl -s -b /tmp/cookies -c /tmp/cookies -o /tmp/register-post.html -w "%{http_code} %{redirect_url}" \
    -X POST "$base_url/Account/Register" \
    --data-urlencode "__RequestVerificationToken=$token" \
    --data-urlencode "Name=Alice" \
    --data-urlencode "Email=$email" \
    --data-urlencode "Password=x")"

curl -s -b /tmp/cookies -c /tmp/cookies "$base_url/Account/Register" -o /tmp/register2.html
token="$(get_token /tmp/register2.html)"

duplicate_status="$(curl -s -b /tmp/cookies -c /tmp/cookies -o /tmp/register-dupe.html -w "%{http_code}" \
    -X POST "$base_url/Account/Register" \
    --data-urlencode "__RequestVerificationToken=$token" \
    --data-urlencode "Name=Alice2" \
    --data-urlencode "Email=$email" \
    --data-urlencode "Password=y")"

duplicate_text="$(grep -o "This email is already registered" /tmp/register-dupe.html || true)"

curl -s -b /tmp/cookies -c /tmp/cookies "$base_url/Account/Login" -o /tmp/login.html
token="$(get_token /tmp/login.html)"

login_status="$(curl -s -b /tmp/cookies -c /tmp/cookies -o /tmp/login-post.html -w "%{http_code} %{redirect_url}" \
    -X POST "$base_url/Account/Login" \
    --data-urlencode "__RequestVerificationToken=$token" \
    --data-urlencode "Email=$email" \
    --data-urlencode "Password=x")"

users_status="$(curl -s -b /tmp/cookies -o /tmp/users.html -w "%{http_code}" "$base_url/Users")"

echo "email=$email"
echo "register=$register_status"
echo "duplicate=$duplicate_status $duplicate_text"
echo "login=$login_status"
echo "users=$users_status"
