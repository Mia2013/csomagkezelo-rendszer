import { ALL_ROLES, ROLES } from "../constants/constants.js";
import Home from "./Home.jsx";
import Login from "./Login.jsx";
import Register from "./Register.jsx";

import CreatePackage from "./CreatePackage.jsx";
import TrackPackage from "./TrackPackage.jsx";

export const allPages = [
  { id: "createPackage", name: "Csomagfeladás", path: "/csomagfeladas",
    component: CreatePackage, showInNavbar: true, roles: ALL_ROLES },
  { id: "trackPackage", name: "Csomagkövetés", path: "/csomagkovetes",
    component: TrackPackage, showInNavbar: true, roles: ALL_ROLES },
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
