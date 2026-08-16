import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    vue({
      // Keep the root layout's direction-contract HTML comment (App.vue)
      // through production minification: Vue's compiler strips template
      // comments by default in prod, which would make the contract
      // unauditable in the shipped build.
      template: { compilerOptions: { comments: true } },
    }),
  ],
})
