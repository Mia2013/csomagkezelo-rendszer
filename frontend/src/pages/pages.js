import { ALL_ROLES, ROLES } from "../constants/constants.js";
import Home from "./Home.jsx";
import Login from "./Login.jsx";
import Register from "./Register.jsx";

export const allPages = [
  {
    id: "home",
    name: "Kezdőlap",
    path: "/",
    component: Home,
    showInNavbar: true,
    roles: ALL_ROLES,
  },
  {
    id: "login",
    name: "Bejelentkezés",
    path: "/login",
    component: Login,
    showInNavbar: true,
    roles: [ROLES.GUEST],
  },
  {
    id: "register",
    name: "Regisztráció",
    path: "/register",
    component: Register,
    showInNavbar: true,
    roles: [ROLES.GUEST],
  },
];
