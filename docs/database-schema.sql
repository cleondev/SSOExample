-- DDL tham khảo cho PostgreSQL. Có thể chuyển sang SQL Server bằng kiểu uniqueidentifier/datetimeoffset tương ứng.
create table users (
    id uuid primary key,
    user_name varchar(100) not null unique,
    email varchar(255) not null unique,
    display_name varchar(255) not null,
    is_active boolean not null default true,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create table user_passwords (
    user_id uuid primary key references users(id),
    password_hash text not null,
    password_algorithm varchar(50) not null,
    password_changed_at timestamptz not null default now(),
    failed_count int not null default 0,
    locked_until timestamptz null
);

create table roles (
    id uuid primary key,
    name varchar(100) not null unique,
    description text null
);

create table user_roles (
    user_id uuid not null references users(id),
    role_id uuid not null references roles(id),
    primary key (user_id, role_id)
);

create table clients (
    id varchar(100) primary key,
    name varchar(255) not null,
    require_pkce boolean not null default true,
    is_active boolean not null default true,
    created_at timestamptz not null default now()
);

create table client_redirect_uris (
    client_id varchar(100) not null references clients(id),
    redirect_uri text not null,
    primary key (client_id, redirect_uri)
);

create table auth_codes (
    code_hash text primary key,
    user_id uuid not null references users(id),
    client_id varchar(100) not null references clients(id),
    redirect_uri text not null,
    code_challenge text null,
    expires_at timestamptz not null,
    consumed_at timestamptz null,
    created_at timestamptz not null default now()
);

create table refresh_tokens (
    id uuid primary key,
    token_hash text not null unique,
    user_id uuid not null references users(id),
    client_id varchar(100) not null references clients(id),
    expires_at timestamptz not null,
    revoked_at timestamptz null,
    replaced_by_token_id uuid null references refresh_tokens(id),
    created_at timestamptz not null default now()
);

create table impersonation_sessions (
    id uuid primary key,
    actor_user_id uuid not null references users(id),
    subject_user_id uuid not null references users(id),
    reason text not null,
    started_at timestamptz not null default now(),
    expires_at timestamptz not null,
    ended_at timestamptz null,
    source_ip varchar(64) not null,
    user_agent text null
);

create table audit_logs (
    id uuid primary key,
    created_at timestamptz not null default now(),
    actor_user_id uuid null references users(id),
    subject_user_id uuid null references users(id),
    action varchar(100) not null,
    client_id varchar(100) null references clients(id),
    impersonation_session_id uuid null references impersonation_sessions(id),
    source_ip varchar(64) null,
    user_agent text null,
    reason text null,
    metadata_json jsonb null
);

create index ix_audit_logs_actor_created_at on audit_logs(actor_user_id, created_at desc);
create index ix_audit_logs_subject_created_at on audit_logs(subject_user_id, created_at desc);
create index ix_refresh_tokens_user_client on refresh_tokens(user_id, client_id);
create index ix_impersonation_sessions_actor on impersonation_sessions(actor_user_id, started_at desc);
