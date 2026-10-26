import { useEffect, useState } from "react";
import {
  Alert, Box, Button, CircularProgress, Dialog, DialogActions,
  DialogContent, DialogTitle, MenuItem, Stack, TextField, Typography,
} from "@mui/material";
import { getCourierCapacity } from "../../api/warehouseApi";
import { getCourierName, getPackageSpace } from "./warehouseHelpers";

const titles = {
  pickup: "Felvétel kiosztása",
  delivery: "Kiszállítás kiosztása",
  reassign: "Futár módosítása",
};

export default function CourierAssignmentDialog({ selection, couriers, busy, error, onClose, onConfirm }) {
  const [courierId, setCourierId] = useState("");
  const [capacity, setCapacity] = useState(null);
  const [capacityError, setCapacityError] = useState("");
  const [checking, setChecking] = useState(false);
  const [menuOpen, setMenuOpen] = useState(false);
  const [capacityList, setCapacityList] = useState({});
  const item = selection.package;
  const space = getPackageSpace(item.size);
  const choices = couriers.filter((courier) => courier.id !== item.courierId);
  const canSave = !!courierId && !busy && !checking && !!capacity?.isActive &&
    space > 0 && capacity.freeCapacity >= space;

  useEffect(() => {
    if (!courierId) return;
    let cancelled = false;
    getCourierCapacity(courierId)
      .then((value) => { if (!cancelled) setCapacity(value); })
      .catch((error) => { if (!cancelled) setCapacityError(error.message); })
      .finally(() => { if (!cancelled) setChecking(false); });
    return () => { cancelled = true; };
  }, [courierId]);

  useEffect(() => {
    if (!menuOpen) return;
    let cancelled = false;
    couriers.filter((courier) => courier.id !== item.courierId).forEach((courier) => {
      getCourierCapacity(courier.id)
        .then((data) => {
          if (!cancelled) setCapacityList((list) => ({ ...list, [courier.id]: { data } }));
        })
        .catch((error) => {
          if (!cancelled) setCapacityList((list) => ({ ...list, [courier.id]: { error: error.message } }));
        });
    });
    return () => { cancelled = true; };
  }, [menuOpen, couriers, item.courierId]);

  function changeCourier(event) {
    setCourierId(Number(event.target.value) || "");
    setCapacity(null);
    setCapacityError("");
    setChecking(!!event.target.value);
  }

  function submit(event) {
    event.preventDefault();
    if (canSave) onConfirm(courierId);
  }

  return (
    <Dialog open fullWidth maxWidth="sm" onClose={() => { if (!busy) onClose(); }} aria-labelledby="courier-dialog-title">
      <Box component="form" onSubmit={submit}>
        <DialogTitle id="courier-dialog-title">{titles[selection.kind]}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ pt: 1 }}>
            <Typography variant="body2" sx={{ overflowWrap: "anywhere" }}>Csomag: {item.trackingNumber}</Typography>
            <Typography variant="body2" color="text.secondary">Méret: {item.size} · Szükséges hely: {space} helyegység</Typography>
            {space === 0 && <Alert severity="warning">A csomag mérete ismeretlen. A kiosztáshoz előbb javítani kell az adatát.</Alert>}
            {choices.length === 0 && <Alert severity="warning">Nincs másik választható aktív futár.</Alert>}
            <TextField select fullWidth label="Futár" value={courierId} onChange={changeCourier} disabled={busy || choices.length === 0}
              slotProps={{ select: {
                onOpen: () => { setCapacityList({}); setMenuOpen(true); },
                onClose: () => setMenuOpen(false),
                renderValue: (value) => getCourierName(couriers, Number(value)),
              } }}>
              <MenuItem value="">Válassz futárt</MenuItem>
              {choices.map((courier) => {
                const info = capacityList[courier.id];
                let label = "Betöltés…";
                if (info?.data) label = `${info.data.freeCapacity} helyegység szabad`;
                if (info?.data && !info.data.isActive) label = "Inaktív futár";
                if (info?.error) label = "Nem érhető el";
                return (
                  <MenuItem key={courier.id} value={courier.id} aria-label={getCourierName(couriers, courier.id)}
                    aria-describedby={`courier-capacity-${courier.id}`} sx={{ gap: 2, justifyContent: "space-between" }}>
                    <Box component="span" sx={{ minWidth: 0, overflow: "hidden", textOverflow: "ellipsis" }}>{getCourierName(couriers, courier.id)}</Box>
                    <Typography component="span" id={`courier-capacity-${courier.id}`} variant="body2" color="text.secondary"
                      title={info?.error || ""} sx={{ ml: "auto", whiteSpace: "nowrap" }}>{label}</Typography>
                  </MenuItem>
                );
              })}
            </TextField>
            {checking && <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}><CircularProgress size={18} /><Typography variant="body2">Szabad hely ellenőrzése…</Typography></Stack>}
            {capacityError && <Alert severity="error">{capacityError}</Alert>}
            {error && <Alert severity="error">{error}</Alert>}
            {capacity && (
              <Alert severity={canSave ? "info" : "warning"}>
                Szabad hely: {capacity.freeCapacity} / {capacity.capacity} helyegység.
                {!capacity.isActive ? " A futár már nem aktív." : capacity.freeCapacity < space ? " A csomag nem fér be az autóba." : ""}
              </Alert>
            )}
          </Stack>
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button onClick={onClose} disabled={busy}>Mégse</Button>
          <Button type="submit" variant="contained" disabled={!canSave}>{busy ? "Mentés…" : "Mentés"}</Button>
        </DialogActions>
      </Box>
    </Dialog>
  );
}
