CREATE TABLE IF NOT EXISTS users (
    id SERIAL PRIMARY KEY,
    name VARCHAR(120) NOT NULL,
    email VARCHAR(320) NOT NULL,
    password_hash TEXT NOT NULL,
    status VARCHAR(20) NOT NULL,
    confirmation_token VARCHAR(80) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL,
    last_login_at TIMESTAMPTZ
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_users_email ON users(LOWER(email));
CREATE UNIQUE INDEX IF NOT EXISTS ux_users_confirmation_token ON users(confirmation_token);
CREATE INDEX IF NOT EXISTS ix_users_last_login_at ON users(last_login_at DESC NULLS LAST, created_at DESC);
