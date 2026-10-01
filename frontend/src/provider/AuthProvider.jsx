import { useContext, createContext, useState, useEffect, useMemo } from "react";
import { useNavigate } from "react-router";
import { jwtDecode } from "jwt-decode";
import { instance, postData, putData, endpoints } from "../api/apiCalls";
import { ROLES } from "../constants/constants";

const AuthContext = createContext();

const AuthProvider = ({ children }) => {
    const [token, setToken] = useState(localStorage.getItem("csomagkezelo_token") || "");
    const [user, setUser] = useState({ role: ROLES.GUEST });
    const [alert, setAlert] = useState();
    const navigate = useNavigate();

    const { isAdmin, isCourier, isCustomer, isWarehouseOperator, isAuthenticated } = useMemo(() => ({
        isAdmin: user?.role === ROLES.ADMIN,
        isCourier: user?.role === ROLES.COURIER,
        isCustomer: user?.role === ROLES.CUSTOMER,
        isWarehouseOperator: user?.role === ROLES.WAREHOUSE_OPERATOR,
        isAuthenticated: !!token
    }), [user, token]);

    const decodeToken = (_token) => {
        try {
            const decoded = jwtDecode(_token);
            setUser(decoded);
            return decoded;
        } catch (error) {
            setAlert({ message: "Érvénytelen munkamenet!", severity: "error" });
            logOut();
        }
    };

    const setupSession = (activeToken) => {
        setToken(activeToken);
        localStorage.setItem("csomagkezelo_token", activeToken);
        instance.defaults.headers.common["Authorization"] = `Bearer ${activeToken}`;
        decodeToken(activeToken);
    };

    const logIn = (username, password) => {
        postData(endpoints.loginUser, { username, password })
            .then(data => {
                setupSession(data.token);
                setAlert({ message: "Sikeres bejelentkezés!", severity: "success" });
            })
            .catch(error => setAlert({ message: error.message || "Hiba a bejelentkezés során!", severity: "error" }));
    };

    const register = (formData) => {
        postData(endpoints.registerUser, formData)
            .then(() => {
                setAlert({ message: "Sikeres regisztráció!", severity: "success" });
                navigate("/login");
            })
            .catch(e => setAlert({ message: e.message || "Hiba a regisztráció során!", severity: "error" }));
    };

    const updateUser = (formData) => {
        putData(`${endpoints.updateUser}/${user.id}`, formData)
            .then(updatedUser => {
                setUser(prev => ({ ...prev, ...updatedUser }));
                setAlert({ message: "Profil sikeresen frissítve!", severity: "success" });
            })
            .catch(error => setAlert({ message: error.message || "Hiba a frissítés során!", severity: "error" }));
    };

    const logOut = () => {
        setToken("");
        localStorage.removeItem("csomagkezelo_token");
        delete instance.defaults.headers.common["Authorization"];
        setUser({ role: ROLES.GUEST });
        navigate("/");
    };

  
    useEffect(() => {
        if (token) {
            setupSession(token);
        } 
        // eslint-disable-next-line 
    }, []);

    return (
        <AuthContext.Provider value={{
            token, user,
            isAdmin, isCourier, isCustomer, isWarehouseOperator, isAuthenticated,
            logIn, logOut, register, updateUser,
            alert, setAlert
        }}>
            {children}
        </AuthContext.Provider>
    );
};

export default AuthProvider;
export const useAuth = () => useContext(AuthContext);