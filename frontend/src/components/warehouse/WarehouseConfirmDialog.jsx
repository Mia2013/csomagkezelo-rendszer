import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, Stack, Typography } from "@mui/material";
import { getCourierName } from "./warehouseHelpers";

export default function WarehouseConfirmDialog({ selection, couriers, waitingCount, busy, error, onClose, onConfirm }) {
  const automatic = selection.kind === "auto";
  return (
    <Dialog open fullWidth maxWidth="sm" onClose={() => { if (!busy) onClose(); }} aria-labelledby="warehouse-confirm-title">
      <DialogTitle id="warehouse-confirm-title">{automatic ? "Automatikus kiszállításkiosztás" : "Bevételezés megerősítése"}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ pt: 1 }}>
          {automatic ? (
            <Typography>{waitingCount} raktári csomag vár kiosztásra. A rendszer az autókat azonosítójuk sorrendjében, egymás után tölti fel, a csomagok mérete és a már foglalt hely alapján. A be nem férő csomagok a várakozó listán maradnak.</Typography>
          ) : (
            <>
              <Typography sx={{ overflowWrap: "anywhere" }}>Csomag: {selection.package.trackingNumber}</Typography>
              <Typography>Beszállító futár: {getCourierName(couriers, selection.package.courierId)}</Typography>
              <Typography variant="body2" color="text.secondary">A bevételezés után a csomag bekerül a raktári készletbe.</Typography>
            </>
          )}
          {error && <Alert severity="error">{error}</Alert>}
        </Stack>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2 }}>
        <Button onClick={onClose} disabled={busy}>Mégse</Button>
        <Button variant="contained" onClick={onConfirm} disabled={busy}>
          {busy ? "Mentés…" : automatic ? "Kiosztás indítása" : "Bevételezés"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
