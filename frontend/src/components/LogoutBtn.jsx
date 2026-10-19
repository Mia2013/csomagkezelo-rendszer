import { Button } from "@mui/material";
import LogoutIcon from '@mui/icons-material/Logout';
import { useAuth } from "../provider/AuthProvider";

const LogoutBtn = () => {
    const { logOut, isAuthenticated } = useAuth();

    if (isAuthenticated) {
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