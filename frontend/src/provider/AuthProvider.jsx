// Régi megoldás:
// // Régi megoldás:
// // import { useContext, createContext, useState, useEffect, useMemo } from "react";
// // Új megoldás:
// import { useContext, useState, useEffect } from "react";
// import { useNavigate } from "react-router";
// import { jwtDecode } from "jwt-decode";
// import { instance, postData, putData, endpoints } from "../api/apiCalls";
// import { ROLES } from "../constants/constants";
// import { AuthContext } from "./AuthContext";
//
//
// const AuthProvider = ({ children }) => {
//     const [token, setToken] = useState(localStorage.getItem("csomagkezelo_token") || "");
//     const [user, setUser] = useState({ role: ROLES.GUEST });
//     const [alert, setAlert] = useState();
//     const navigate = useNavigate();
//
//     // Régi megoldás:
//     // const { isAdmin, isCourier, isCustomer, isWarehouseOperator, isAuthenticated } = useMemo(() => ({
//     //     isAdmin: user?.role === ROLES.ADMIN,
//     //     isCourier: user?.role === ROLES.COURIER,
//     //     isCustomer: user?.role === ROLES.CUSTOMER,
//     //     isWarehouseOperator: user?.role === ROLES.WAREHOUSE_OPERATOR,
//     //     isAuthenticated: !!token
//     // }), [user, token]);
//     // Új megoldás:
//     const isAdmin = user?.role === ROLES.ADMIN;
//     const isCourier = user?.role === ROLES.COURIER;
//     const isCustomer = user?.role === ROLES.CUSTOMER;
//     const isWarehouseOperator = user?.role === ROLES.WAREHOUSE_OPERATOR;
//     const isAuthenticated = !!token;
//
//     const decodeToken = (_token) => {
//         try {
//             const decoded = jwtDecode(_token);
//             setUser(decoded);
//             return decoded;
//         } catch (error) {
//             logOut();
//             setAlert({ message: "Érvénytelen munkamenet!", severity: "error" });
//         }
//     };
//
//     const setupSession = (activeToken) => {
//         setToken(activeToken);
//         localStorage.setItem("csomagkezelo_token", activeToken);
//         instance.defaults.headers.common["Authorization"] = `Bearer ${activeToken}`;
//         decodeToken(activeToken);
//     };
//
//     const logIn = (username, password) => {
//         postData(endpoints.loginUser, { username, password })
//             .then(data => {
//                 setupSession(data.token);
//                 setAlert({ message: "Sikeres bejelentkezés!", severity: "success" });
//             })
//             .catch(error => setAlert({ message: error.message || "Hiba a bejelentkezés során!", severity: "error" }));
//     };
//
//     const register = (formData) => {
//         postData(endpoints.registerUser, formData)
//             .then(() => {
//                 setAlert({ message: "Sikeres regisztráció!", severity: "success" });
//                 navigate("/#belepes");
//             })
//             .catch(e => setAlert({ message: e.message || "Hiba a regisztráció során!", severity: "error" }));
//     };
//
//     const updateUser = (formData) => {
//         putData(`${endpoints.updateUser}/${user.id}`, formData)
//             .then(updatedUser => {
//                 setUser(prev => ({ ...prev, ...updatedUser }));
//                 setAlert({ message: "Profil sikeresen frissítve!", severity: "success" });
//             })
//             .catch(error => setAlert({ message: error.message || "Hiba a frissítés során!", severity: "error" }));
//     };
//
//     const logOut = () => {
//         setToken("");
//         localStorage.removeItem("csomagkezelo_token");
//         delete instance.defaults.headers.common["Authorization"];
//          setAlert({ message: "Sikeres kijelentkezés!", severity: "info" });
//         setUser({ role: ROLES.GUEST });
//         navigate("/");
//     };
//
//
//     useEffect(() => {
//         if (token) {
//             setupSession(token);
//         }
//         // eslint-disable-next-line
//     }, []);
//
//     return (
//         <AuthContext.Provider value={{
//             token, user,
//             isAdmin, isCourier, isCustomer, isWarehouseOperator, isAuthenticated,
//             logIn, logOut, register, updateUser,
//             alert, setAlert
//         }}>
//             {children}
//         </AuthContext.Provider>
//     );
// };
//
// export default AuthProvider;
// export const useAuth = () => useContext(AuthContext);
// Új megoldás:
import { useEffect, useState } from "react";
import { useNavigate } from "react-router";
import { jwtDecode } from "jwt-decode";
import { postData, putData, endpoints } from "../api/apiCalls";
import { ROLES } from "../constants/constants";
import { AuthContext } from "./AuthContext";

function readUser(token) {
  if (!token) return null;
  try {
    const user = jwtDecode(token);
    if (!Number.isInteger(Number(user.id)) || Number(user.id) <= 0 || typeof user.exp !== "number" ||
        !Number.isFinite(user.exp) || user.exp * 1000 <= Date.now()) return null;
    return user;
  } catch {
    return null;
  }
}

const AuthProvider = ({ children }) => {
  const [token, setToken] = useState(() => localStorage.getItem("csomagkezelo_token") || "");
  const [user, setUser] = useState(() => readUser(token));
  const [alert, setAlert] = useState(() => token && !user
    ? { message: "Érvénytelen vagy lejárt munkamenet! Jelentkezz be újra, vagy lépj ki a vendégfeladáshoz.", severity: "error" }
    : undefined);
  const navigate = useNavigate();
  const hasSession = !!token;
  const isAuthenticated = user != null;
  const isAdmin = isAuthenticated && user.role === ROLES.ADMIN;
  const isCourier = isAuthenticated && user.role === ROLES.COURIER;
  const isCustomer = isAuthenticated && user.role === ROLES.CUSTOMER;
  const isWarehouseOperator = isAuthenticated && user.role === ROLES.WAREHOUSE_OPERATOR;

  useEffect(() => {
    if (!user) return;
    const timer = setTimeout(() => {
      setUser(null);
      setAlert({ message: "A munkamenet lejárt. Jelentkezz be újra, vagy lépj ki a vendégfeladáshoz.", severity: "error" });
    }, Math.max(0, user.exp * 1000 - Date.now()));
    return () => clearTimeout(timer);
  }, [user]);

  function setupSession(activeToken) {
    const activeUser = readUser(activeToken);
    if (!activeUser) throw new Error("Érvénytelen vagy lejárt munkamenet!");
    localStorage.setItem("csomagkezelo_token", activeToken);
    setToken(activeToken);
    setUser(activeUser);
  }

  function logIn(username, password) {
    return postData(endpoints.loginUser, { username, password }, true)
      .then((data) => {
        setupSession(data.token);
        setAlert({ message: "Sikeres bejelentkezés!", severity: "success" });
      })
      .catch((error) => setAlert({ message: error.message || "Hiba a bejelentkezés során!", severity: "error" }));
  }

  function register(formData) {
    return postData(endpoints.registerUser, formData, true)
      .then(() => {
        setAlert({ message: "Sikeres regisztráció!", severity: "success" });
        navigate("/#belepes");
      })
      .catch((error) => setAlert({ message: error.message || "Hiba a regisztráció során!", severity: "error" }));
  }

  function updateUser(formData) {
    return putData(endpoints.updateUser + "/" + user.id, formData)
      .then((updatedUser) => {
        setUser((current) => ({ ...current, ...updatedUser }));
        setAlert({ message: "Profil sikeresen frissítve!", severity: "success" });
      })
      .catch((error) => setAlert({ message: error.message || "Hiba a frissítés során!", severity: "error" }));
  }

  function logOut() {
    localStorage.removeItem("csomagkezelo_token");
    setToken("");
    setUser(null);
    setAlert({ message: "Sikeres kijelentkezés!", severity: "info" });
    navigate("/");
  }

  return (
    <AuthContext.Provider value={{
      token, user: user || { role: ROLES.GUEST }, hasSession,
      isAuthenticated, isAdmin, isCourier, isCustomer, isWarehouseOperator,
      logIn, logOut, register, updateUser, alert, setAlert,
    }}>
      {children}
    </AuthContext.Provider>
  );
};

export default AuthProvider;
