const session = { accessToken: localStorage.getItem('jquery.accessToken'), refreshToken: localStorage.getItem('jquery.refreshToken') };
function show(value) { $('#output').text(JSON.stringify(value, null, 2)); }
function saveTokens(response) {
  session.accessToken = response.accessToken;
  session.refreshToken = response.refreshToken;
  localStorage.setItem('jquery.accessToken', response.accessToken);
  localStorage.setItem('jquery.refreshToken', response.refreshToken);
  show(response);
}
function api(path, options = {}) {
  return $.ajax({ url: path, method: options.method || 'GET', contentType: 'application/json', data: options.body && JSON.stringify(options.body), headers: session.accessToken ? { Authorization: `Bearer ${session.accessToken}` } : {} });
}
$('#login').on('click', () => api('/api/auth/login/password', { method: 'POST', body: { userNameOrEmail: $('#username').val(), password: $('#password').val(), clientId: 'jquery-spa' } }).then(saveTokens).catch(show));
$('#loginAs').on('click', () => api('/api/auth/login-as', { method: 'POST', body: { targetUserId: $('#targetUser').val(), reason: $('#reason').val(), clientId: 'jquery-spa' } }).then(saveTokens).catch(show));
$('#loadMe').on('click', () => api('/api/me').then(show).catch(show));
$('#loadOrders').on('click', () => api('/api/orders').then(show).catch(show));
