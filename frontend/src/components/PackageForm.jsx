import { useEffect, useRef, useState } from "react";
import { Link } from "react-router";
import {
  Alert,
  Box,
  Button,
  IconButton,
  MenuItem,
  Paper,
  Stack,
  TextField,
  Tooltip,
  Typography,
} from "@mui/material";
import ContentCopy from "@mui/icons-material/ContentCopy";
import Person from "@mui/icons-material/Person";
import PersonPinCircle from "@mui/icons-material/PersonPinCircle";
import Inventory2 from "@mui/icons-material/Inventory2";
import LocalShipping from "@mui/icons-material/LocalShipping";

import { useAuth } from "../provider/AuthContext";
import { endpoints, getData, postData } from "../api/apiCalls";
import dayjs from "../utils/dayjs";

const getEarliestPickupDate = () =>
  dayjs().tz("Europe/Budapest").add(1, "day").format("YYYY-MM-DD");

const initialData = {
  senderName: "",
  senderAddress: "",
  senderPhoneNumber: "",
  recipientName: "",
  recipientAddress: "",
  recipientPhoneNumber: "",
  recipientEmail: "",
  value: "",
  weight: "",
  size: "M",
  pickupDate: "",
  pickupSlot: "09:00-12:00",
};

const SectionTitle = ({ icon, children }) => (
  <Stack direction="row" spacing={1} sx={{ alignItems: "center", mb: 2 }}>
    {icon}
    <Typography variant="h6" component="h2">
      {children}
    </Typography>
  </Stack>
);

const PackageForm = () => {
  const { isAuthenticated, user, setAlert } = useAuth();

  const [data, setData] = useState(initialData);
  const [fieldErrors, setFieldErrors] = useState({});
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState(null);
  const sendingRef = useRef(false);

  // Régi megoldás:
  // const getUserById = async (id) => {
  //   try {
  //     const profile = await getData(`${endpoints.getUserById}/${id}`);
  //     setData((current) => ({
  //       ...current,
  //       senderName: `${profile.lastName} ${profile.firstName}`,
  //       senderAddress: profile.address || "",
  //       senderPhoneNumber: profile.phoneNumber || "",
  //     }));
  //   } catch {
  //     setError("A feladó adatait nem sikerült betölteni, töltsd ki kézzel!");
  //   }
  // };
  //
  // useEffect(() => {
  //   if (isAuthenticated) {
  //     getUserById(user.id);
  //   }
  // }, [isAuthenticated, user?.id]);
  // Új megoldás:
  const userId = user?.id;
  useEffect(() => {
    if (!isAuthenticated || !userId) return;
    let cancelled = false;
    getData(endpoints.getUserById + "/" + userId)
      .then((profile) => {
        if (cancelled) return;
        setData((current) => ({
          ...current,
          senderName: (profile.lastName + " " + profile.firstName).trim(),
          senderAddress: profile.address || "",
          senderPhoneNumber: profile.phoneNumber || "",
        }));
      })
      .catch(() => {
        if (!cancelled) setError("A feladó adatait nem sikerült betölteni, töltsd ki kézzel!");
      });
    return () => { cancelled = true; };
  }, [isAuthenticated, userId]);

  const handleChange = (e) =>
    setData({ ...data, [e.target.name]: e.target.value });

  const validateForm = (data) => {
    const errors = {};

    if (!data.senderName.trim()) {
      errors.senderName = "Feladó neve kötelező!";
    }
    if (!data.senderAddress.trim()) {
      errors.senderAddress = "Feladó címe kötelező!";
    }
    if (!data.senderPhoneNumber.trim()) {
      errors.senderPhoneNumber = "Feladó telefonszáma kötelező!";
    }
    if (!data.recipientName.trim()) {
      errors.recipientName = "Címzett neve kötelező!";
    }
    if (!data.recipientAddress.trim()) {
      errors.recipientAddress = "Címzett címe kötelező!";
    }
    if (!data.recipientPhoneNumber.trim()) {
      errors.recipientPhoneNumber = "Címzett telefonszáma kötelező!";
    }
    if (!data.recipientEmail.trim()) {
      errors.recipientEmail = "Címzett e-mail-címe kötelező!";
    }
    if (data.value === "") {
      errors.value = "Csomag értéke kötelező!";
    }
    if (data.weight < 1 || data.weight > 30000) {
      errors.weight = "A tömeg 1 és 30 000 gramm között legyen!";
    }
    if (!data.pickupDate) {
      errors.pickupDate = "Felvétel napja kötelező!";
    } else if (data.pickupDate < getEarliestPickupDate()) {
      errors.pickupDate = "A felvétel legkorábban holnapra kérhető!";
    }
    return errors;
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (sendingRef.current) return;

    const errors = validateForm(data);
    setFieldErrors(errors);
    setError("");
    if (Object.keys(errors).length > 0) return;

    sendingRef.current = true;
    setBusy(true);
    try {
      const created = await postData(endpoints.createPackage, {
        ...data,
        value: Number(data.value),
        weight: Number(data.weight),
      });
      setResult(created);
    } catch (error) {
      setError(error.message);
      setFieldErrors(error.errors || {});
    } finally {
      sendingRef.current = false;
      setBusy(false);
    }
  };

  const handleCopy = async () => {
    try {
      await navigator.clipboard.writeText(result.trackingNumber);
      setAlert({
        message: "Az azonosítót a vágólapra másoltuk.",
        severity: "success",
      });
    } catch {
      setAlert({
        message:
          "A másolás nem sikerült, jelöld ki és másold ki az azonosítót!",
        severity: "error",
      });
    }
  };

  const handleNewPackage = () => setResult(null);

  const fieldProps = (name) => ({
    name,
    value: data[name],
    onChange: handleChange,
    disabled: busy,
    fullWidth: true,
    error: !!fieldErrors[name],
    helperText: fieldErrors[name],
  });

  return (
    <Paper sx={{ p: 4, my: 4 }}>
      <Typography variant="h5" component="h1" gutterBottom align="center">
        Csomag előjegyzése
      </Typography>

      {result ? (
        <Stack spacing={2} sx={{ maxWidth: 520, mx: "auto", mt: 2 }}>
          <Alert severity="success">
            Sikeres előjegyzés! Őrizd meg a csomagazonosítót.
          </Alert>
          <Stack
            direction="row"
            spacing={1}
            sx={{ alignItems: "center", justifyContent: "center" }}
          >
            <Typography variant="h6" component="p">
              {result.trackingNumber}
            </Typography>
            <Tooltip title="Azonosító másolása">
              <IconButton onClick={handleCopy} aria-label="Azonosító másolása">
                <ContentCopy />
              </IconButton>
            </Tooltip>
          </Stack>
          <Button
            component={Link}
            variant="contained"
            to={`/?trackingNumber=${result.trackingNumber}`}
          >
            Csomag követése
          </Button>
          <Button onClick={handleNewPackage}>Új csomag előjegyzése</Button>
        </Stack>
      ) : (
        <Box component="form" onSubmit={handleSubmit} noValidate>
          {!isAuthenticated && (
            <Typography align="center" color="text.secondary" sx={{ mb: 3 }}>
              Vendégként is feladhatsz csomagot. Ha bejelentkezel, a feladás a
              fiókodhoz kapcsolódik, és a feladó adatait automatikusan
              kitöltjük.
            </Typography>
          )}

          <Box
            sx={{
              display: "grid",
              gridTemplateColumns: { xs: "1fr", md: "1fr 1.5fr 1fr" },
              gap: 4,
            }}
          >
            <Stack spacing={2}>
              <SectionTitle icon={<Person color="primary" />}>
                Feladó
              </SectionTitle>
              <TextField label="Feladó neve*" {...fieldProps("senderName")} />
              <TextField
                label="Feladó címe*"
                {...fieldProps("senderAddress")}
              />
              <TextField
                label="Feladó telefonszáma*"
                type="tel"
                {...fieldProps("senderPhoneNumber")}
              />
            </Stack>

            <Stack spacing={2}>
              <SectionTitle icon={<PersonPinCircle color="primary" />}>
                Címzett
              </SectionTitle>
              <TextField
                label="Címzett neve*"
                {...fieldProps("recipientName")}
              />
              <TextField
                label="Címzett címe*"
                {...fieldProps("recipientAddress")}
              />
              <Stack direction="row" spacing={2}>
                <TextField
                  label="Címzett telefonszáma*"
                  type="tel"
                  {...fieldProps("recipientPhoneNumber")}
                />
                <TextField
                  label="Címzett e-mail-címe*"
                  type="email"
                  {...fieldProps("recipientEmail")}
                />
              </Stack>
            </Stack>

            <Stack spacing={2}>
              <SectionTitle icon={<Inventory2 color="primary" />}>
                Csomag
              </SectionTitle>
              <TextField
                label="Csomag értéke (Ft)*"
                type="number"
                {...fieldProps("value")}
              />
              <Stack direction="row" spacing={2}>
                <TextField select label="Méret" {...fieldProps("size")}>
                  <MenuItem value="S">S</MenuItem>
                  <MenuItem value="M">M</MenuItem>
                  <MenuItem value="L">L</MenuItem>
                </TextField>
                <TextField
                  label="Tömeg (gramm)*"
                  type="number"
                  {...fieldProps("weight")}
                />
              </Stack>
              <Stack direction="row" spacing={2}>
                <TextField
                  label="Felvétel napja*"
                  type="date"
                  {...fieldProps("pickupDate")}
                  slotProps={{
                    inputLabel: { shrink: true },
                    htmlInput: { min: getEarliestPickupDate() },
                  }}
                />
                <TextField select label="Idősáv" {...fieldProps("pickupSlot")}>
                  <MenuItem value="09:00-12:00">09:00-12:00</MenuItem>
                  <MenuItem value="12:00-16:00">12:00-16:00</MenuItem>
                </TextField>
              </Stack>
            </Stack>
          </Box>

          {error && (
            <Alert severity="error" sx={{ mt: 3 }}>
              {error}
            </Alert>
          )}

          <Box sx={{ textAlign: "center", mt: 3 }}>
            <Button
              type="submit"
              variant="contained"
              disabled={busy}
              startIcon={<LocalShipping />}
              sx={{ py: 1.5, px: 6 }}
            >
              {busy ? "Mentés folyamatban…" : "Csomag előjegyzése"}
            </Button>
          </Box>
        </Box>
      )}
    </Paper>
  );
};

export default PackageForm;
