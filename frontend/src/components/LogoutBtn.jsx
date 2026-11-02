import { Button } from "@mui/material";
import LogoutIcon from '@mui/icons-material/Logout';
import { useAuth } from "../provider/AuthContext";

const LogoutBtn = () => {
    const { logOut, hasSession } = useAuth();

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