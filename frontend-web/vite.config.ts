import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import Components from 'unplugin-vue-components/vite'
import { ElementPlusResolver } from 'unplugin-vue-components/resolvers'

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
    // Element Plus bajo demanda (AUDIT.md H-21): solo entran al bundle los componentes y directivas (v-loading) que
    // las plantillas usan. Antes `app.use(ElementPlus)` metía la librería completa (chunk `index` de 769 KB).
    // Los estilos se siguen cargando completos desde main.ts: así ElMessage/ElMessageBox, que se importan por
    // código y no desde una plantilla, no pierden su CSS.
    //
    // dts: false a propósito. El components.d.ts generado registra los el-* como componentes globales TIPADOS y
    // vue-tsc empieza a exigir tipos estrictos en los slots (p. ej. la fila de <el-table> pasa a ser DefaultRow y
    // rompe ~10 plantillas que antes no estaban tipadas). Sin él, el tipado queda igual que antes del cambio.
    Components({
      resolvers: [ElementPlusResolver({ importStyle: false })],
      dts: false,
    }),
  ],
})
