import { createRouter, createWebHistory } from 'vue-router'
import ChatPage from '../page/ChatPage.vue'
import InputPage from '../page/InputPage.vue'

const routes = [
  { path: '/', name: 'chat', component: ChatPage },
  { path: '/input', name: 'input', component: InputPage }
]

const router = createRouter({ history: createWebHistory(), routes })

// 通过 provide/inject 传递全局数据
let gkrResult = null

export const setGkrResult = (result) => {
  gkrResult = result
}

export const getGkrResult = () => gkrResult

export default router
