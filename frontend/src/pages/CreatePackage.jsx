import { useRef, useState } from "react";
import { Link } from "react-router";
import { Alert, Button, Container, Paper, Stack, Typography } from "@mui/material";
import PackageForm from "../components/PackageForm";
import { endpoints, postData } from "../api/apiCalls";

export default function CreatePackage() {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [errors, setErrors] = useState({});
  const [result, setResult] = useState(null);
  const [copyMessage, setCopyMessage] = useState("");
  const sending = useRef(false);

  async function create(data) {
    if (sending.current) return;
    sending.current = true;
    setBusy(true);
    setError("");
    setErrors({});
    try { setResult(await postData(endpoints.createPackage, data)); }
    catch (error) {
      setError(error.message);
      setErrors(error.errors || {});
    } finally { sending.current = false; setBusy(false); }
  }

  async function copy() {
    try {
      await navigator.clipboard.writeText(result.trackingNumber);
      setCopyMessage("Az azonosítót a vágólapra másoltuk.");
    } catch { setCopyMessage("A másolás nem sikerült; jelöld ki és másold ki az azonosítót."); }
  }

  return <Container maxWidth="sm"><Paper sx={{ p: { xs: 2, sm: 4 }, my: 4 }}>
    <Typography variant="h4" component="h1" gutterBottom>Csomag előjegyzése</Typography>
    {result ? <Stack spacing={2}>
      <Alert severity="success">Sikeres előjegyzés! Őrizd meg a csomagazonosítót.</Alert>
      <Typography sx={{ overflowWrap: "anywhere" }}>{result.trackingNumber}</Typography>
      <Button onClick={copy}>Azonosító másolása</Button>
      {copyMessage && <Typography role="status">{copyMessage}</Typography>}
      <Button component={Link} variant="contained"
        to={`/csomagkovetes?trackingNumber=${result.trackingNumber}`}>Csomag követése</Button>
      <Button onClick={() => { setResult(null); setCopyMessage(""); }}>Új csomag előjegyzése</Button>
    </Stack> : <>
      <Typography sx={{ mb: 3 }}>Vendégként is feladhatsz csomagot. Belépés után a feladás a fiókodhoz kapcsolódik.</Typography>
      <PackageForm onSubmit={create} busy={busy} error={error} errors={errors} />
    </>}
  </Paper></Container>;
}
