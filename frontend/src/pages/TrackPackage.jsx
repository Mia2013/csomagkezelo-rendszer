import { useEffect, useState } from "react";
import { useSearchParams } from "react-router";
import { Alert, Box, Button, Container, Paper, TextField, Typography } from "@mui/material";
import { endpoints, getData } from "../api/apiCalls";

const statusNames = {
  PreBooked: "Előjegyezve",
  InTransitToWarehouse: "Úton a raktárba",
  AtWarehouse: "Raktárban",
  AssignedToCourier: "Futárhoz rendelve",
  Delivered: "Kézbesítve",
};

export default function TrackPackage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const trackingNumber = searchParams.get("trackingNumber") || "";
  const [input, setInput] = useState(trackingNumber);
  const [attempt, setAttempt] = useState(0);
  const [state, setState] = useState({ busy: false, result: null, error: "" });
  const [inputError, setInputError] = useState("");

  useEffect(() => {
    if (!trackingNumber) return;
    let active = true;
    async function load() {
      setState({ busy: true, result: null, error: "" });
      try {
        // Nyilvános kereséshez nem küldünk bejelentkezési tokent.
        const result = await getData(endpoints.trackPackage, { trackingNumber }, true);
        if (active) setState({ busy: false, result, error: "" });
      } catch (error) {
        if (active) setState({ busy: false, result: null, error: error.message });
      }
    }
    load();
    return () => { active = false; };
  }, [trackingNumber, attempt]);

  function search(event) {
    event.preventDefault();
    const number = input.trim();
    if (!number) { setInputError("Add meg a csomagazonosítót!"); return; }
    setInputError("");
    setSearchParams({ trackingNumber: number });
    setAttempt(value => value + 1);
  }

  return <Container maxWidth="sm"><Paper sx={{ p: { xs: 2, sm: 4 }, my: 4 }}>
    <Typography variant="h4" component="h1" gutterBottom>Csomag követése</Typography>
    <Typography sx={{ mb: 3 }}>A kereséshez csak a csomagazonosítóra van szükség.</Typography>
    <Box component="form" onSubmit={search} sx={{ display: "grid", gap: 2 }}>
      <TextField label="Csomagazonosító" value={input} onChange={event => setInput(event.target.value)}
        error={!!inputError} helperText={inputError} disabled={state.busy} />
      <Button type="submit" variant="contained" disabled={state.busy}>
        {state.busy ? "Keresés…" : "Keresés"}
      </Button>
    </Box>
    {state.error && <Alert sx={{ mt: 3 }} severity="error">{state.error}</Alert>}
    {state.result && <Box sx={{ mt: 3 }} aria-live="polite">
      <Typography sx={{ overflowWrap: "anywhere" }}>{state.result.trackingNumber}</Typography>
      <Typography variant="h6">{statusNames[state.result.status] || state.result.status}</Typography>
      <Typography>{new Date(state.result.changedAt).toLocaleString("hu-HU", { timeZone: "Europe/Budapest" })} (budapesti idő)</Typography>
    </Box>}
  </Paper></Container>;
}
