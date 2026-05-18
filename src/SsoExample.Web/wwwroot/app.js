// SSOExample jQuery SPA
// - Hash-router cho 4 view: home, signin, dashboard, admin, about
// - Authorization Code + PKCE qua endpoint /api/sso/authorize của demo API
// - Login password local + login-as cho admin
// - Lưu session vào sessionStorage; banner đỏ khi đang impersonate

const STORAGE_KEY = 'ssoexample.session.v2';
const PKCE_KEY = 'ssoexample.pkce.v2';

const config = {
  apiBaseUrl: 'https://localhost:5001',
  clientId: 'ssoexample-web',
  redirectUri: window.location.origin + '/auth/callback',
  authority: null,
  scopes: []
};

const state = {
  accessToken: null,
  refreshToken: null,
  expiresAt: null,
  user: null,
  impersonation: null
};

// ---------- session ----------
function loadSession() {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY);
    if (!raw) return;
    Object.assign(state, JSON.parse(raw));
  } catch { /* ignore */ }
}
function persistSession() {
  if (!state.accessToken) {
    sessionStorage.removeItem(STORAGE_KEY);
    return;
  }
  sessionStorage.setItem(STORAGE_KEY, JSON.stringify(state));
}
function clearSession() {
  state.accessToken = null;
  state.refreshToken = null;
  state.expiresAt = null;
  state.user = null;
  state.impersonation = null;
  sessionStorage.removeItem(STORAGE_KEY);
}
function applyTokenResponse(response) {
  state.accessToken = response.accessToken;
  state.refreshToken = response.refreshToken;
  state.expiresAt = response.expiresAt;
  state.user = response.user;
  state.impersonation = response.impersonation || null;
  persistSession();
}

// ---------- API client ----------
function api(path, options = {}) {
  return $.ajax({
    url: `${config.apiBaseUrl}${path}`,
    method: options.method || 'GET',
    contentType: 'application/json',
    data: options.body && JSON.stringify(options.body),
    headers: state.accessToken ? { Authorization: `Bearer ${state.accessToken}` } : {}
  });
}
function apiError(xhr) {
  const msg = xhr.responseJSON?.error || xhr.statusText || 'Lỗi không xác định';
  return `${xhr.status} · ${msg}`;
}

// ---------- PKCE ----------
function base64UrlEncode(buf) {
  return btoa(String.fromCharCode(...new Uint8Array(buf)))
    .replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}
async function createPkce() {
  const verifier = base64UrlEncode(crypto.getRandomValues(new Uint8Array(32)));
  const hash = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(verifier));
  return { verifier, challenge: base64UrlEncode(hash), state: base64UrlEncode(crypto.getRandomValues(new Uint8Array(16))) };
}

// ---------- UI helpers ----------
function toast(message, type = 'info') {
  $('.toast').remove();
  const $el = $('<div class="toast"></div>').addClass(type).text(message).appendTo('body');
  requestAnimationFrame(() => $el.addClass('show'));
  setTimeout(() => $el.removeClass('show'), 3000);
  setTimeout(() => $el.remove(), 3400);
}
function escapeHtml(value) {
  return $('<div>').text(value ?? '').html();
}
function rolesHtml(roles) {
  return (roles || []).map(r => `<span class="badge role-${escapeHtml(r)}">${escapeHtml(r)}</span>`).join('');
}
function statusBadge(status) {
  return `<span class="badge status-${escapeHtml(status)}">${escapeHtml(status)}</span>`;
}
function decodeJwt(token) {
  try {
    const parts = token.split('.');
    if (parts.length !== 3) return null;
    const padded = parts[1].replace(/-/g, '+').replace(/_/g, '/');
    return JSON.parse(decodeURIComponent(escape(atob(padded.padEnd(padded.length + (4 - padded.length % 4) % 4, '=')))));
  } catch { return null; }
}

// ---------- Router ----------
const routes = {
  '': renderHome,
  '/': renderHome,
  '/signin': renderSignin,
  '/dashboard': renderDashboard,
  '/admin': renderAdmin,
  '/about': renderAbout
};

function currentRoute() {
  const hash = window.location.hash.replace(/^#/, '');
  return (hash.split('?')[0]) || '/';
}

function navigate(path) {
  if (window.location.hash === `#${path}`) {
    render();
  } else {
    window.location.hash = `#${path}`;
  }
}

function render() {
  const path = currentRoute();
  const handler = routes[path] || renderHome;

  $('.nav a').removeClass('active');
  const route = path.split('/')[1] || 'home';
  $(`.nav a[data-route="${route}"]`).addClass('active');

  renderUserPill();
  renderImpersonationBanner();
  handler();
}

window.addEventListener('hashchange', render);

// ---------- Header / banner ----------
function renderUserPill() {
  const $pill = $('#userPill');
  const $sign = $('#signInButton');
  if (!state.user) {
    $pill.attr('hidden', true);
    $sign.removeAttr('hidden');
    return;
  }
  $sign.attr('hidden', true);
  $pill.removeAttr('hidden');
  const initials = (state.user.displayName || state.user.userName || '?').split(/\s+/).map(p => p[0]).join('').slice(0, 2).toUpperCase();
  $pill.find('.avatar').text(initials);
  $pill.find('.user-name').text(state.user.displayName || state.user.userName);
  $pill.find('.user-role').text((state.user.roles || []).join(', ') || 'user');

  $('.nav a[data-role="Admin"]').toggle((state.user.roles || []).includes('Admin'));
}
function renderImpersonationBanner() {
  const $banner = $('#impersonationBanner');
  if (!state.impersonation) {
    $banner.attr('hidden', true);
    return;
  }
  const expires = new Date(state.impersonation.expiresAt).toLocaleTimeString();
  $banner.find('.text').text(
    `Bạn đang login as ${state.user?.displayName || state.user?.userName} · actor: ${state.impersonation.actorUserName} · hết hạn ${expires}`
  );
  $banner.removeAttr('hidden');
}

// ---------- Views ----------
function mount(templateId) {
  const tpl = document.getElementById(templateId);
  $('#view').empty().append(tpl.content.cloneNode(true));
}

function renderHome() {
  mount('tpl-home');
}

function renderSignin() {
  mount('tpl-signin');

  $('#ssoStart').on('click', startSsoFlow);

  $('#passwordForm').on('submit', e => {
    e.preventDefault();
    $('#signinError').attr('hidden', true);
    api('/api/auth/login/password', {
      method: 'POST',
      body: {
        userNameOrEmail: $('#username').val(),
        password: $('#password').val(),
        clientId: config.clientId
      }
    }).then(response => {
      applyTokenResponse(response);
      toast('Đăng nhập thành công');
      navigate('/dashboard');
    }).catch(xhr => {
      $('#signinError').text(`Đăng nhập thất bại: ${apiError(xhr)}`).removeAttr('hidden');
    });
  });
}

async function startSsoFlow() {
  try {
    const pkce = await createPkce();
    sessionStorage.setItem(PKCE_KEY, JSON.stringify(pkce));
    const params = new URLSearchParams({
      client_id: config.clientId,
      redirect_uri: config.redirectUri,
      state: pkce.state,
      code_challenge: pkce.challenge,
      response_type: 'code',
      scope: 'openid profile email'
    });
    window.location.href = `${config.apiBaseUrl}/api/sso/authorize?${params.toString()}`;
  } catch (err) {
    toast('Không khởi tạo được PKCE: ' + err.message, 'error');
  }
}

function handleSsoCallback(search) {
  const params = new URLSearchParams(search);
  const code = params.get('code');
  const stateParam = params.get('state');
  const stored = JSON.parse(sessionStorage.getItem(PKCE_KEY) || 'null');

  history.replaceState(null, '', '/');

  if (!code || !stored || stored.state !== stateParam) {
    toast('Callback không hợp lệ (state mismatch hoặc thiếu code)', 'error');
    navigate('/signin');
    return;
  }

  api('/api/sso/token', {
    method: 'POST',
    body: {
      code,
      redirectUri: config.redirectUri,
      clientId: config.clientId,
      codeVerifier: stored.verifier
    }
  }).then(response => {
    applyTokenResponse(response);
    sessionStorage.removeItem(PKCE_KEY);
    toast('Sign in with Microsoft thành công (demo)');
    navigate('/dashboard');
  }).catch(xhr => {
    toast('Đổi code thất bại: ' + apiError(xhr), 'error');
    navigate('/signin');
  });
}

function renderDashboard() {
  if (!state.accessToken) {
    mount('tpl-401');
    return;
  }
  mount('tpl-dashboard');
  $('#greetName').text(state.user?.displayName || state.user?.userName || 'bạn');
  $('#refreshDashboard').on('click', loadDashboardData);
  $('#copyToken').on('click', () => {
    navigator.clipboard.writeText(state.accessToken).then(() => toast('Đã copy token'));
  });
  loadDashboardData();
}

function loadDashboardData() {
  api('/api/me')
    .then(me => {
      const decoded = decodeJwt(state.accessToken) || {};
      const rows = [
        ['User ID', me.userId],
        ['Username', me.userName],
        ['Roles', (me.roles || []).join(', ') || '(none)'],
        ['Issuer (iss)', decoded.iss || '—'],
        ['Audience (aud)', decoded.aud || '—'],
        ['Expires (exp)', decoded.exp ? new Date(decoded.exp * 1000).toLocaleString() : '—']
      ];
      $('#meProps').html(rows.map(([k, v]) => `<dt>${escapeHtml(k)}</dt><dd>${escapeHtml(v)}</dd>`).join(''));
      $('#tokenView').text(JSON.stringify(decoded, null, 2));
    })
    .catch(xhr => $('#meProps').html(`<dt>Lỗi</dt><dd>${escapeHtml(apiError(xhr))}</dd>`));

  api('/api/orders')
    .then(orders => {
      const body = orders.length
        ? orders.map(o => `<tr><td>${o.id}</td><td>${escapeHtml(o.owner)}</td><td>${o.total.toFixed(2)}</td><td>${statusBadge(o.status)}</td></tr>`).join('')
        : '<tr><td colspan="4" class="muted">Không có đơn hàng nào</td></tr>';
      $('#ordersTable tbody').html(body);
    })
    .catch(xhr => $('#ordersTable tbody').html(`<tr><td colspan="4" class="muted">Lỗi: ${escapeHtml(apiError(xhr))}</td></tr>`));
}

function renderAdmin() {
  if (!state.accessToken) { mount('tpl-401'); return; }
  if (!(state.user?.roles || []).includes('Admin')) { mount('tpl-403'); return; }
  mount('tpl-admin');

  $('#refreshAdmin').on('click', loadAdminData);
  $('#loginAsForm').on('submit', e => {
    e.preventDefault();
    const reason = $('#reason').val().trim();
    if (reason.length < 10) {
      toast('Lý do tối thiểu 10 ký tự', 'error');
      return;
    }
    api('/api/auth/login-as', {
      method: 'POST',
      body: { targetUserId: $('#targetUser').val(), reason, clientId: config.clientId }
    }).then(response => {
      applyTokenResponse(response);
      toast(`Đang login as ${response.user.displayName}`);
      navigate('/dashboard');
    }).catch(xhr => toast('Login-as thất bại: ' + apiError(xhr), 'error'));
  });

  loadAdminData();
}

function loadAdminData() {
  api('/api/admin/users')
    .then(users => {
      const candidates = users.filter(u => !(u.roles || []).includes('Admin'));
      $('#targetUser').html(
        candidates.map(u => `<option value="${escapeHtml(u.id)}">${escapeHtml(u.displayName)} (${escapeHtml(u.userName)})</option>`).join('')
      );
      $('#usersTable tbody').html(
        users.map(u => `<tr><td><strong>${escapeHtml(u.displayName)}</strong><br><span class="muted small">${escapeHtml(u.userName)}</span></td><td>${escapeHtml(u.email)}</td><td>${rolesHtml(u.roles)}</td></tr>`).join('')
      );
    })
    .catch(xhr => $('#usersTable tbody').html(`<tr><td colspan="3" class="muted">Lỗi: ${escapeHtml(apiError(xhr))}</td></tr>`));

  api('/api/admin/audit-logs')
    .then(logs => {
      const body = logs.length
        ? logs.map(l => `<tr>
            <td>${new Date(l.createdAt).toLocaleString()}</td>
            <td><span class="badge">${escapeHtml(l.action)}</span></td>
            <td>${escapeHtml(l.actorUserId)}</td>
            <td>${escapeHtml(l.subjectUserId || '—')}</td>
            <td>${escapeHtml(l.reason || '—')}</td>
            <td>${escapeHtml(l.ipAddress)}</td>
          </tr>`).join('')
        : '<tr><td colspan="6" class="muted">Chưa có audit log</td></tr>';
      $('#auditTable tbody').html(body);
    })
    .catch(xhr => $('#auditTable tbody').html(`<tr><td colspan="6" class="muted">Lỗi: ${escapeHtml(apiError(xhr))}</td></tr>`));
}

function renderAbout() {
  mount('tpl-about');
}

// ---------- Bootstrap ----------
function bindGlobalEvents() {
  $('#logout').on('click', () => {
    clearSession();
    toast('Đã đăng xuất');
    navigate('/signin');
  });
  $('#endImpersonation').on('click', () => {
    clearSession();
    toast('Đã kết thúc phiên login-as');
    navigate('/signin');
  });
}

$(async function () {
  loadSession();

  try {
    const appConfig = await $.getJSON('/config');
    config.apiBaseUrl = appConfig.api?.baseUrl || config.apiBaseUrl;
    config.clientId = appConfig.api?.localDemoClientId || config.clientId;
    config.authority = appConfig.microsoftEntraId?.authority;
    config.scopes = appConfig.microsoftEntraId?.scopes || [];
    if (appConfig.microsoftEntraId?.redirectUris?.[0]) {
      config.redirectUri = appConfig.microsoftEntraId.redirectUris[0];
    }
  } catch { /* config endpoint optional */ }

  bindGlobalEvents();

  if (window.location.pathname === '/auth/callback' && new URLSearchParams(window.location.search).has('code')) {
    handleSsoCallback(window.location.search);
    return;
  }

  render();
});
