import { Button } from "@mui/material";
import LogoutIcon from '@mui/icons-material/Logout';
// Régi megoldás:
// import { useAuth } from "../provider/AuthProvider";
// Új megoldás:
import { useAuth } from "../provider/AuthContext";

const LogoutBtn = () => {
    // Régi megoldás:
    // const { logOut, isAuthenticated } = useAuth();
    // Új megoldás:
    const { logOut, hasSession } = useAuth();

    // Régi megoldás:
    // if (isAuthenticated) {
    // Új megoldás:
    if (hasSession) {
        return (
            <Button
                variant="text"
                color="error"
                startIcon={<LogoutIcon />}
                onClick={logOut}
            >
                Kilépés
            </Button>)
    }
}

export default LogoutBtn