export const environment = {
  production: false,
  /** Tum API cagrilari icin taban yol; dev'de proxy.conf.json -> http://localhost:5210 */
  apiBaseUrl: '/api',
  /** Bos ise Turnstile widget'i hic render edilmez ve login'e null token gonderilir. */
  turnstileSiteKey: '',
};
