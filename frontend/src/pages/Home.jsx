import { Box, Typography } from "@mui/material";
import { useAuth } from "../provider/AuthProvider";

const Home = () => {
  const { isAuthenticated, user } = useAuth();

  return (
    <Box sx={{ textAlign: "center", mt: 4 }}>
      <Typography variant="h4" component="h1">
        Csomagkezelő Rendszer
      </Typography>
      <Typography variant="h6" sx={{ mt: 2, color: "text.secondary" }}>
        {isAuthenticated
          ? `Üdv, ${user?.firstName}! Sikeresen bejelentkeztél.`
          : "Jelentkezz be vagy regisztrálj a folytatáshoz"}
      </Typography>
    </Box>
  );
};

export default Home;