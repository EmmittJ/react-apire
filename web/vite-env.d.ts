/// <reference types="vite/client" />

interface ViteTypeOptions {
  strictImportMetaEnv: unknown
}

interface ImportMetaEnv {
  readonly VITE_APP_DEPLOYMENT: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}

declare const runtime: {
  env: ImportMetaEnv
}