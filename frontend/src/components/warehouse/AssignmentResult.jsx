import { Alert, Box, IconButton, List, ListItem, ListItemText, Paper, Stack, Typography } from "@mui/material";
import Close from "@mui/icons-material/Close";
import { getCourierName } from "./warehouseHelpers";

export default function AssignmentResult({ result, couriers, onClose }) {
  return (
    <Paper variant="outlined" sx={{ p: 2.5, borderRadius: 3 }}>
      <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center" }}>
        <Typography component="h2" variant="h6">Kiosztás eredménye</Typography>
        <IconButton aria-label="Kiosztási eredmény bezárása" onClick={onClose}><Close /></IconButton>
      </Stack>
      <Alert severity={result.assigned.length > 0 ? "success" : "info"} sx={{ mt: 1 }}>
        Kiosztva: {result.assigned.length} csomag. Kiosztatlan maradt: {result.unassigned.length} csomag.
      </Alert>
      <Stack direction={{ xs: "column", md: "row" }} spacing={3} sx={{ mt: 2 }}>
        {result.assigned.length > 0 && (
          <Box sx={{ flex: 1, minWidth: 0 }}>
            <Typography sx={{ fontWeight: 700 }}>Futárhoz rendelve</Typography>
            <List dense>
              {result.assigned.map((item) => (
                <ListItem key={item.id} disableGutters>
                  <ListItemText primary={item.trackingNumber} secondary={getCourierName(couriers, item.courierId)}
                    sx={{ overflowWrap: "anywhere" }} />
                </ListItem>
              ))}
            </List>
          </Box>
        )}
        {result.unassigned.length > 0 && (
          <Box sx={{ flex: 1, minWidth: 0 }}>
            <Typography sx={{ fontWeight: 700 }}>Raktárosi döntésre vár</Typography>
            <List dense>
              {result.unassigned.map((item) => (
                <ListItem key={item.id} disableGutters>
                  <ListItemText primary={item.trackingNumber} secondary={item.reason} sx={{ overflowWrap: "anywhere" }} />
                </ListItem>
              ))}
            </List>
          </Box>
        )}
      </Stack>
    </Paper>
  );
}
