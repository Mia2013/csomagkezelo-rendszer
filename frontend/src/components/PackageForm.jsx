import { useState } from "react";
import { Alert, Box, Button, MenuItem, TextField, Typography } from "@mui/material";

const initialData = {
  senderName: "", senderAddress: "",
  recipientName: "", recipientAddress: "",
  value: "", weight: "", size: "M", pickupDate: "",
};

const contactFields = [
  { name: "senderName", label: "Feladó neve", length: 150 },
  { name: "senderAddress", label: "Feladó címe", length: 300 },
  { name: "recipientName", label: "Címzett neve", length: 150 },
  { name: "recipientAddress", label: "Címzett címe", length: 300 },
];

export default function PackageForm({ onSubmit, busy, error, errors }) {
  const [data, setData] = useState(initialData);
  function change(event) {
    setData({ ...data, [event.target.name]: event.target.value });
  }
  function submit(event) {
    event.preventDefault();
    if (!busy) onSubmit({ ...data, value: Number(data.value), weight: Number(data.weight) });
  }

  return <Box component="form" onSubmit={submit} sx={{ display: "grid", gap: 2 }}>
    <Typography variant="h6">Feladó és címzett</Typography>
    {contactFields.map(field => <TextField key={field.name} required
      name={field.name} label={field.label}
      value={data[field.name]} onChange={change} disabled={busy}
      slotProps={{ htmlInput: { maxLength: field.length } }}
      error={!!errors[field.name]} helperText={errors[field.name]} />)}

    <Typography variant="h6">Csomag és felvétel</Typography>
    <TextField required name="value" label="Csomag értéke (Ft)" type="number"
      value={data.value} onChange={change} disabled={busy}
      slotProps={{ htmlInput: { min: 0, max: 2147483647, step: 1 } }}
      error={!!errors.value} helperText={errors.value} />
    <TextField required name="weight" label="Tömeg (gramm)" type="number"
      value={data.weight} onChange={change} disabled={busy}
      slotProps={{ htmlInput: { min: 1, max: 30000, step: 1 } }}
      error={!!errors.weight} helperText={errors.weight || "Legfeljebb 30 000 g (30 kg)."} />
    <TextField select name="size" label="Méret" value={data.size} onChange={change} disabled={busy}
      error={!!errors.size} helperText={errors.size}>
      {["S", "M", "L"].map(size => <MenuItem key={size} value={size}>{size}</MenuItem>)}
    </TextField>
    <TextField required name="pickupDate" label="Felvétel napja" type="date"
      value={data.pickupDate} onChange={change} disabled={busy}
      slotProps={{ inputLabel: { shrink: true } }} error={!!errors.pickupDate}
      helperText={errors.pickupDate || "Legkorábban a következő budapesti naptári nap."} />
    {error && <Alert severity="error">{error}</Alert>}
    <Button variant="contained" type="submit" disabled={busy}>
      {busy ? "Mentés folyamatban…" : "Csomag előjegyzése"}
    </Button>
  </Box>;
}
