import { Outlet } from "react-router";
import { Box } from "@mui/material";
import Nav from "../components/Nav";
import CustomAlert from "../components/CustomAlert";
// Régi megoldás:
// import { useAuth } from "../provider/AuthProvider";
// Új megoldás:
import { useAuth } from "../provider/AuthContext";

const Layout = () => {
  const { alert, setAlert } = useAuth();
  return (
    <Box sx={{ display: "flex", flexDirection: "column", minHeight: "100vh" }}>
      <Nav />
      <main>
        <Outlet />
      </main>
      {alert && (
        <CustomAlert
          alert={alert}
          setAlert={setAlert}
          onClose={() => setAlert(null)}
        />
      )}
    </Box>
  );
};

export default Layout;
