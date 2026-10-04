import { Box, Button, Stack, Typography } from "@mui/material";
import { Link } from "react-router";
import { useAuth } from "../provider/AuthContext";

export default function Home() {
  const { isAuthenticated, user } = useAuth();
  return <Box sx={{ textAlign: "center", m: 4 }}>
    <Typography variant="h4" component="h1">Csomagkezelő rendszer</Typography>
    <Typography sx={{ my: 3 }}>{isAuthenticated ? `Üdv, ${user.firstName}!` :
      "Csomagfeladás és követés regisztráció nélkül is."}</Typography>
    <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ justifyContent: "center" }}>
      <Button component={Link} to="/csomagfeladas" variant="contained">Csomag előjegyzése</Button>
      <Button component={Link} to="/csomagkovetes" variant="outlined">Csomag követése</Button>
    </Stack>
  </Box>;
}
