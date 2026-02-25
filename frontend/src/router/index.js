import { createRouter, createWebHistory } from "vue-router";
import InputPage from "../pages/InputPage.vue";
import ChatPage from "../pages/ChatPage.vue";

const routes = [
  {
    path: "/",
    component: InputPage
  },
  {
    path: "/chat",
    component: ChatPage
  }
];

const router = createRouter({
  history: createWebHistory(),
  routes
});

export default router;
