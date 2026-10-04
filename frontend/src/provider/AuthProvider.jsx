import { useState } from "react";
import { useNavigate } from "react-router";
import { jwtDecode } from "jwt-decode";
import { postData, endpoints } from "../api/apiCalls";
import { ROLES } from "../constants/constants";
import { AuthContext } from "./AuthContext";

const guest = { token: "", user: { role: ROLES.GUEST }, expired: false };

function readSession() {
  const token = localStorage.getItem("csomagkezelo_token");
  if (token) {
    try {
      const user = jwtDecode(token);
      return { token, user, expired: !(user.exp > Date.now() / 1000) };
    } catch {
      // Nem váltunk csendben vendégfeladásra: a felhasználó lépjen ki vagy be.
      return { token, user: { role: ROLES.GUEST }, expired: true };
    }
  }
  return guest;
}

export default function AuthProvider({ children }) {
  const [session, setSession] = useState(readSession);
  const [alert, setAlert] = useState(session.expired
    ? { message: "A bejelentkezés lejárt vagy érvénytelen. Jelentkezz be újra, vagy lépj ki a vendégfeladáshoz.", severity: "warning" }
    : null);
  const navigate = useNavigate();

  async function logIn(userName, password) {
    try {
      const data = await postData(endpoints.loginUser, { userName, password }, true);
      const user = jwtDecode(data.token);
      localStorage.setItem("csomagkezelo_token", data.token);
      setSession({ token: data.token, user, expired: false });
      setAlert({ message: "Sikeres bejelentkezés!", severity: "success" });
      navigate("/");
    } catch (error) { setAlert({ message: error.message, severity: "error" }); }
  }

  async function register(formData) {
    try {
      await postData(endpoints.registerUser, formData, true);
      setAlert({ message: "Sikeres regisztráció! Most jelentkezz be.", severity: "success" });
      navigate("/login");
    } catch (error) {
      const details = Object.values(error.errors || {}).join(" ");
      setAlert({ message: details || error.message, severity: "error" });
    }
  }

  function logOut() {
    localStorage.removeItem("csomagkezelo_token");
    setSession(guest);
    setAlert({ message: "Sikeres kijelentkezés!", severity: "info" });
    navigate("/");
  }

  return <AuthContext.Provider value={{
    ...session, isAuthenticated: !!session.token && !session.expired, hasSession: !!session.token,
    isAdmin: session.user.role === ROLES.ADMIN,
    isCustomer: session.user.role === ROLES.CUSTOMER,
    isCourier: session.user.role === ROLES.COURIER,
    isWarehouseOperator: session.user.role === ROLES.WAREHOUSE_OPERATOR,
    logIn, logOut, register, alert, setAlert,
  }}>{children}</AuthContext.Provider>;
}
