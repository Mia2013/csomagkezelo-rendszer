import { useEffect } from "react";
import { Link, useLocation } from "react-router";
import {
  Avatar,
  Box,
  Chip,
  Container,
  Grid,
  Paper,
  Stack,
  Typography,
} from "@mui/material";
import Inventory2 from "@mui/icons-material/Inventory2";
import EditNote from "@mui/icons-material/EditNote";
import LocalShipping from "@mui/icons-material/LocalShipping";
import Warehouse from "@mui/icons-material/Warehouse";
import HomeWork from "@mui/icons-material/HomeWork";
import Schedule from "@mui/icons-material/Schedule";
import Person from "@mui/icons-material/Person";
import ViewInArOutlined from "@mui/icons-material/ViewInArOutlined";

import { useAuth } from "../provider/AuthContext";
import TrackPackageForm from "../components/TrackPackageForm";
import LoginForm from "../components/LoginForm";

const steps = [
  {
    icon: <EditNote />,
    title: "1. Előjegyzés",
    text: "Add meg a csomag adatait online, akár regisztráció nélkül is.",
  },
  {
    icon: <LocalShipping />,
    title: "2. Felvétel futárral",
    text: "A futár a választott napon és idősávban elviszi a csomagot.",
  },
  {
    icon: <Warehouse />,
    title: "3. Raktár",
    text: "A csomag a központi raktárba kerül, onnan indul tovább.",
  },
  {
    icon: <HomeWork />,
    title: "4. Kézbesítés",
    text: "A futár házhoz viszi a csomagot a címzettnek.",
  },
];

const chips = [
  { icon: <Inventory2 />, title: "Méretek:", text: "S, M, L · max. 30 kg" },
  {
    icon: <Schedule />,
    title: "Felvétel:",
    text: "9–12 vagy 12–16 óra, legkorábban másnap",
  },
  {
    icon: <Person />,
    title: "Regisztrációval:",
    text: "feladói adatok, előzmények",
  },
];

const ActionTile = ({ to, icon, title, text }) => (
  <Paper
    component={Link}
    to={to}
    elevation={0}
    sx={{
      p: 2,
      borderRadius: 3,
      textDecoration: "none",
      bgcolor: "rgba(255,255,255,0.10)",
      color: "common.white",
      border: "1px solid rgba(255,255,255,0.45)",
      transition: "background-color 200ms, color 200ms, box-shadow 200ms",
      "& .MuiAvatar-root": {
        bgcolor: "rgba(255,255,255,0.18)",
        color: "common.white",
        transition: "inherit",
      },
      "& .MuiTypography-body2": { color: "#e3f2fd", transition: "inherit" },
      "&:hover, &:focus-visible": {
        bgcolor: "common.white",
        color: "text.primary",
        boxShadow: 6,
        "& .MuiAvatar-root": { bgcolor: "#e3f2fd", color: "primary.dark" },
        "& .MuiTypography-body2": { color: "text.secondary" },
      },
    }}
  >
    <Stack direction="row" spacing={2} sx={{ alignItems: "center" }}>
      <Avatar variant="rounded" sx={{ width: 40, height: 40 }}>
        {icon}
      </Avatar>
      <Box>
        <Typography
          variant="subtitle1"
          component="span"
          sx={{ display: "block", fontWeight: 700 }}
        >
          {title}
        </Typography>
        <Typography variant="body2">{text}</Typography>
      </Box>
    </Stack>
  </Paper>
);

const Home = () => {
  const { isAuthenticated, user } = useAuth();
  const { hash } = useLocation();

  useEffect(() => {
    if (hash)
      document
        .getElementById(hash.slice(1))
        ?.scrollIntoView({ behavior: "smooth" });
    else window.scrollTo({ top: 0, behavior: "smooth" });
  }, [hash, isAuthenticated]);

  return (
    <>
      <Box sx={{ bgcolor: "primary.dark", color: "common.white" }}>
        <Container maxWidth="lg" sx={{ py: { xs: 3, md: 4 } }}>
          <Grid container spacing={6} sx={{ alignItems: "center" }}>
            <Grid size={{ xs: 12, md: 4 }}>
              <Stack spacing={2}>
                <ActionTile
                  to="/csomagfeladas"
                  icon={<LocalShipping />}
                  title="Csomag feladása"
                  text="Előjegyzés pár perc alatt"
                />
                {!isAuthenticated && (
                  <ActionTile
                    to="/register"
                    icon={<Person />}
                    title="Regisztráció"
                    text="A feladó adatai maguktól kitöltődnek"
                  />
                )}
              </Stack>
            </Grid>

            <Grid size={{ xs: 12, md: 8 }}>
              <Stack
                direction="row"
                spacing={2}
                sx={{ alignItems: "center", mb: 2 }}
              >
                <Avatar
                  sx={{
                    width: 48,
                    height: 48,
                    bgcolor: "common.white",
                    color: "primary.dark",
                  }}
                >
                  <ViewInArOutlined />
                </Avatar>
                <Box>
                  <Typography
                    variant="h4"
                    component="h1"
                    sx={{ fontWeight: 700 }}
                  >
                    Kövesd a csomagod egy kattintással
                  </Typography>
                  <Typography sx={{ color: "#e3f2fd" }}>
                    {isAuthenticated
                      ? `Üdv, ${user.firstName}!`
                      : "Csomagfeladás és követés regisztráció nélkül is."}
                  </Typography>
                </Box>
              </Stack>
              <TrackPackageForm key={String(isAuthenticated)} />
            </Grid>
          </Grid>
        </Container>
      </Box>

      <Container maxWidth="lg" sx={{ py: 5 }}>
        <Typography variant="h5" component="h2" align="center" sx={{ mb: 3 }}>
          Hogyan működik?
        </Typography>
        <Grid container spacing={3}>
          {steps.map((step) => (
            <Grid key={step.title} size={{ xs: 12, sm: 6, md: 3 }}>
              <Stack
                spacing={1}
                sx={{ alignItems: "center", textAlign: "center" }}
              >
                <Avatar
                  sx={{
                    width: 48,
                    height: 48,
                    bgcolor: "#e3f2fd",
                    color: "primary.dark",
                  }}
                >
                  {step.icon}
                </Avatar>
                <Typography
                  variant="subtitle1"
                  component="h3"
                  sx={{ fontWeight: 500 }}
                >
                  {step.title}
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  {step.text}
                </Typography>
              </Stack>
            </Grid>
          ))}
        </Grid>

        <Stack
          direction="row"
          useFlexGap
          spacing={1.5}
          sx={{ flexWrap: "wrap", justifyContent: "center", mt: 4 }}
        >
          {chips.map((chip) => (
            <Chip
              key={chip.title}
              icon={chip.icon}
              variant="outlined"
              color="primary"
              label={
                <Typography
                  variant="body2"
                  component="span"
                  sx={{ color: "text.primary" }}
                >
                  <strong>{chip.title}</strong> {chip.text}
                </Typography>
              }
              sx={{ bgcolor: "background.paper" }}
            />
          ))}
        </Stack>

        {!isAuthenticated && (
          <Box
            id="belepes"
            sx={{
              bgcolor: "#e3f2fd",
              borderRadius: 4,
              p: { xs: 2, md: 3 },
              mt: 4,
              scrollMarginTop: 96,
            }}
          >
            <Grid container spacing={3} sx={{ alignItems: "center" }}>
              <Grid size={{ xs: 12, md: 7 }}>
                <Typography
                  variant="h5"
                  component="h2"
                  sx={{ color: "#0d2b52", mb: 1 }}
                >
                  Van már fiókod?
                </Typography>
                <Typography sx={{ color: "#3b4a5c" }}>
                  Lépj be, és a feladó adatai maguktól kitöltődnek, a feladás
                  pedig a fiókodhoz kapcsolódik.
                </Typography>
              </Grid>
              <Grid size={{ xs: 12, md: 5 }}>
                <LoginForm />
              </Grid>
            </Grid>
          </Box>
        )}
      </Container>
    </>
  );
};

export default Home;
