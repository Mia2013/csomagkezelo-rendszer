import { ALL_ROLES, ROLES } from "../constants/constants.js";
import Home from "./Home.jsx";
import Register from "./Register.jsx";

import CreatePackage from "./CreatePackage.jsx";
import MyPackages from "./MyPackages.jsx";
import WarehousePage from "./Warehouse.jsx";

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
    id: "register",
    name: "Regisztráció",
    path: "/register",
    component: Register,
    showInNavbar: true,
    roles: [ROLES.GUEST],
  },
  {
    id: "createPackage",
    name: "Csomagfeladás",
    path: "/csomagfeladas",
    component: CreatePackage,
    showInNavbar: true,
    roles: ALL_ROLES,
  },
  {
    id: "myPackages",
    name: "Csomagjaim",
    path: "/csomagjaim",
    component: MyPackages,
    showInNavbar: true,
    roles: ALL_ROLES,
  },
  {
    id: "warehouse",
    name: "Raktár",
    path: "/raktar",
    component: WarehousePage,
    showInNavbar: true,
    roles: [ROLES.WAREHOUSE_OPERATOR, ROLES.ADMIN],
  },
];
