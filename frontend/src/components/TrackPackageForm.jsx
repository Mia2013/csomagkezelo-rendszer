import { useEffect, useState } from "react";
import { useSearchParams } from "react-router";
import { Alert, Button, Stack, TextField } from "@mui/material";
import Search from "@mui/icons-material/Search";

import { endpoints, getData } from "../api/apiCalls";
import StatusTimeline from "./StatusTimeline";

const TrackPackageForm = () => {
  const [searchParams, setSearchParams] = useSearchParams();
  const trackingNumber = searchParams.get("trackingNumber") || "";

  const [input, setInput] = useState(trackingNumber);
  const [result, setResult] = useState(null);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

  const trackPackage = async (number) => {
    setBusy(true);
    setResult(null);
    setError("");
    try {
      setResult(
        await getData(endpoints.trackPackage, { trackingNumber: number }, true),
      );
    } catch (error) {
      setError(error.message);
    } finally {
      setBusy(false);
    }
  };

  useEffect(() => {
    if (trackingNumber) {
      trackPackage(trackingNumber);
    }
  }, []);

  const handleSubmit = (e) => {
    e.preventDefault();

    const number = input.trim();
    if (!number) {
      setError("Add meg a csomagazonosítót!");
      return;
    }
    setSearchParams({ trackingNumber: number });
    trackPackage(number);
  };

  return (
    <Stack spacing={3}>
      <Stack
        component="form"
        onSubmit={handleSubmit}
        direction="row"
        spacing={1.5}
      >
        <TextField
          fullWidth
          size="small"
          placeholder="Csomagazonosító"
          value={input}
          onChange={(e) => setInput(e.target.value)}
          disabled={busy}
          slotProps={{ htmlInput: { "aria-label": "Csomagazonosító" } }}
          sx={{
            bgcolor: "common.white",
            borderRadius: 2,
            "& fieldset": { border: 0 },
          }}
        />
        <Button
          type="submit"
          variant="contained"
          disabled={busy}
          startIcon={<Search />}
          sx={{
            px: 3,
            fontWeight: 700,
            bgcolor: "#ff9800",
            color: "#1f2937",
            "&:hover": { bgcolor: "#fb8c00" },
          }}
        >
          {busy ? "Keresés…" : "Keresés"}
        </Button>
      </Stack>

      {error && <Alert severity="error">{error}</Alert>}

      <StatusTimeline result={result} />
    </Stack>
  );
};

export default TrackPackageForm;
