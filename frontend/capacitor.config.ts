import type { CapacitorConfig } from '@capacitor/cli';

const isDev = process.env.NODE_ENV !== 'production';
const devServerIp = process.env.DEV_SERVER_IP || '192.168.1.100';

const config: CapacitorConfig = {
  appId: 'com.synapse.app',
  appName: 'Synapse',
  webDir: 'out',
  server: isDev
    ? { url: `http://${devServerIp}:3000`, cleartext: true }
    : undefined,
  android: { allowMixedContent: true },
  ios: { contentInset: 'automatic' },
};

export default config;
