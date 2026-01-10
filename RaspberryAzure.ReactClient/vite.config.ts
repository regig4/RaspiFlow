import {defineConfig, loadEnv} from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig(({mode}) => {
    const env = loadEnv(mode, process.cwd(), '');

    return {
        plugins: [react()],
        server: {
            port: parseInt(env.VITE_PORT),
            proxy: {
                '/api': {
                    target: process.env.services__agent__https__0 ||
                        process.env.services__agent__http__0,
                    changeOrigin: true,
                    rewrite: (path) => path.replace(/^\/api/, ''),
                    secure: false,
                }
            }
        }
    }
})
