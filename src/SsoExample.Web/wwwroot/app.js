const config = {
  apiBaseUrl: 'https://localhost:5001',
  clientId: 'ssoexample-web'
};
const session = {
  accessToken: localStorage.getItem('web.jquery.accessToken'),
  refreshToken: localStorage.getItem('web.jquery.refreshToken')
};
function show(value) { $('#output').text(JSON.stringify(value, null, 2)); }
function saveTokens(response) {
  session.accessToken = response.accessToken;
  session.refreshToken = response.refreshToken;
  localStorage.setItem('web.jquery.accessToken', response.accessToken);
  localStorage.setItem('web.jquery.refreshToken', response.refreshToken);
  show(response);
}
function api(path, options = {}) {
  return $.ajax({
    url: `${config.apiBaseUrl}${path}`,
    method: options.method || 'GET',
    contentType: 'application/json',
    data: options.body && JSON.stringify(options.body),
    headers: session.accessToken ? { Authorization: `Bearer ${session.accessToken}` } : {}
  });
}
function bindEvents() {
  $('#login').on('click', () => api('/api/auth/login/password', {
    method: 'POST',
    body: { userNameOrEmail: $('#username').val(), password: $('#password').val(), clientId: config.clientId }
  }).then(saveTokens).catch(show));
  $('#loginAs').on('click', () => api('/api/auth/login-as', {
    method: 'POST',
    body: { targetUserId: $('#targetUser').val(), reason: $('#reason').val(), clientId: config.clientId }
  }).then(saveTokens).catch(show));
  $('#loadMe').on('click', () => api('/api/me').then(show).catch(show));
  $('#loadOrders').on('click', () => api('/api/orders').then(show).catch(show));
}
$.getJSON('/config')
  .then(appConfig => {
    config.apiBaseUrl = appConfig.api?.baseUrl || config.apiBaseUrl;
    config.clientId = appConfig.api?.localDemoClientId || config.clientId;
  })
  .always(bindEvents);
