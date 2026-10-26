import { Alert, Button, Container, Stack } from "@mui/material";
import { Link } from "react-router";
import { ROLES } from "../constants/constants";
import { useAuth } from "../provider/AuthContext";
import WarehouseDashboard from "../components/warehouse/WarehouseDashboard";

export default function Warehouse() {
  const { user, isAuthenticated } = useAuth();
  if (!isAuthenticated) {
    return (
      <Container sx={{ py: 5 }}>
        <Stack spacing={2} sx={{ alignItems: "flex-start" }}>
          <Alert severity="info">A raktárkezeléshez jelentkezz be raktárosként vagy adminisztrátorként.</Alert>
          <Button component={Link} to="/#belepes" variant="contained">Bejelentkezés</Button>
        </Stack>
      </Container>
    );
  }
  if (![ROLES.WAREHOUSE_OPERATOR, ROLES.ADMIN].includes(user?.role)) {
    return <Container sx={{ py: 5 }}><Alert severity="error">Ehhez az oldalhoz raktáros vagy adminisztrátor jogosultság szükséges.</Alert></Container>;
  }
  return <WarehouseDashboard key={user.id} />;
}
