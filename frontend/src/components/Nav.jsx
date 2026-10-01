import { useMemo } from "react";
import {
  AppBar,
  Container,
  Box,
  Button,
  Toolbar,
  Typography,
} from "@mui/material";
import { Link, useLocation } from "react-router";
import { useAuth } from "../provider/AuthProvider";
import LogoutBtn from "./LogoutBtn";
import { allPages } from "../pages/pages";
import { ROLES } from "../constants/constants";

const Navigation = () => {
  const location = useLocation();
  const { user, isAuthenticated, displayName } = useAuth();
  const homePage = allPages.filter((q) => q.path === "/")[0];

  const navPages = useMemo(() => {
    const currentRole = user?.role || ROLES.GUEST;
    return allPages.filter(
      (page) =>
        page.showInNavbar && page.roles.includes(currentRole),
    );
  }, [user]);

  return (
    <AppBar
      position="sticky"
      elevation={2}
      sx={{
        backgroundColor: "rgba(245, 245, 245, 0.8)",
        backdropFilter: "blur(10px)",
      }}
    >
      <Container maxWidth="xl">
        <Toolbar disableGutters sx={{ justifyContent: "space-between" }}>
          <Box sx={{ display: "flex", alignItems: "center", gap: 2 }}>
            <Button
              component={Link}
              to={homePage?.path || "/"}
              sx={{
                my: 1,
                color: "primary.main",
                fontWeight: 800,
              }}
            >
              Pannon Post
            </Button>
          {isAuthenticated && (
              <Typography variant="body2" sx={{ color: "text.secondary" }}>
                {user?.firstName}
              </Typography>
            )}
            <LogoutBtn />
          </Box>

          <Box sx={{ display: "flex", gap: 1 }}>
            {navPages.map((page) => (
              <Button
                key={page.name}
                component={Link}
                to={page.path}
                sx={{
                  my: 1,
                  color:
                    location.pathname === page.path
                      ? "primary.main"
                      : "text.primary",
                  fontWeight: location.pathname === page.path ? 800 : 500,
                }}
              >
                {page.name}
              </Button>
            ))}
          </Box>
        </Toolbar>
      </Container>
    </AppBar>
  );
};

export default Navigation;
