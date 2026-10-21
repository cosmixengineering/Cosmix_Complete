import { beforeAll } from 'vitest'
import { setProjectAnnotations } from '@storybook/react-vite'
import * as projectAnnotations from './preview'
import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'

i18n.use(initReactI18next).init({
  lng: 'en',
  resources: {},
  fallbackLng: false,
  interpolation: { escapeValue: false },
})

const annotations = setProjectAnnotations([projectAnnotations])

beforeAll(annotations.beforeAll)
