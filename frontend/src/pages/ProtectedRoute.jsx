import { useEffect } from "react";
import { Navigate, useLocation } from "react-router";
import { useAuth } from "../provider/AuthContext";
import { ROLES } from "../constants/constants";

const ProtectedRoute = ({ roles, children }) => {
  const { user, isAuthenticated, setAlert } = useAuth();
  const location = useLocation();
  const role = user?.role || ROLES.GUEST;
  const allowed = roles.includes(role);
  const guestOnly = roles.includes(ROLES.GUEST);

  useEffect(() => {
    if (isAuthenticated && !allowed && !guestOnly) {
      setAlert({
        message: "Nincs jogosultságod az oldal megtekintéséhez!",
        severity: "warning",
      });
    }
  }, [isAuthenticated, allowed, guestOnly, setAlert]);

  if (allowed) {
    return children;
  }

  if (!isAuthenticated) {
    return <Navigate to="/#belepes"  state={{ from: location }} />;
  }

  return <Navigate to="/"  />;
};

export default ProtectedRoute;
