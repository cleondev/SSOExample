function App() {
  const [token, setToken] = React.useState(localStorage.getItem('react.accessToken') || '');
  const [output, setOutput] = React.useState('Chưa có dữ liệu.');
  const headers = token ? { Authorization: `Bearer ${token}` } : {};
  async function call(path, options = {}) {
    const response = await fetch(path, { ...options, headers: { 'Content-Type': 'application/json', ...headers, ...(options.headers || {}) } });
    const payload = await response.json();
    setOutput(JSON.stringify(payload, null, 2));
    return payload;
  }
  async function passwordLogin() {
    const payload = await call('/api/auth/login/password', { method: 'POST', body: JSON.stringify({ userNameOrEmail: 'alice', password: 'Alice@123', clientId: 'react-spa' }) });
    if (payload.accessToken) { localStorage.setItem('react.accessToken', payload.accessToken); setToken(payload.accessToken); }
  }
  return <main className="shell">
    <h1>React SPA</h1>
    <p>SPA này dùng cùng SSO API với jQuery SPA nhưng client_id khác.</p>
    <section className="panel">
      <button onClick={passwordLogin}>Login as Alice bằng password</button>{' '}
      <button onClick={() => call('/api/me')}>Gọi /api/me</button>{' '}
      <button onClick={() => call('/api/orders')}>Gọi /api/orders</button>
    </section>
    <section className="panel"><h2>Kết quả</h2><pre>{output}</pre></section>
  </main>;
}
ReactDOM.createRoot(document.getElementById('root')).render(<App />);
