import {
  Box, Button, Chip, Link as MuiLink, Stack, Table, TableBody,
  TableCell, TableContainer, TableHead, TableRow, Typography,
} from "@mui/material";
import { Link } from "react-router";
import Inventory2Outlined from "@mui/icons-material/Inventory2Outlined";
import dayjs from "../../utils/dayjs";
import { getCourierName, statusLabels } from "./warehouseHelpers";

export default function WarehousePackageTable({ packages, couriers, waitingIds, disabled, onAction }) {
  function actionFor(item) {
    if (item.canReceive) return { kind: "receive", label: "Bevételezés" };
    if (item.currentStatus === "PreBooked" && !item.courierId) return { kind: "pickup", label: "Felvétel kiosztása" };
    if (waitingIds.has(item.id)) return { kind: "delivery", label: "Kiszállítás kiosztása" };
    if (item.canReassign) return { kind: "reassign", label: "Futár módosítása" };
    return null;
  }

  if (packages.length === 0) {
    return (
      <Stack spacing={1} sx={{ py: 7, color: "text.secondary", alignItems: "center" }}>
        <Inventory2Outlined sx={{ fontSize: 40 }} />
        <Typography component="h2" variant="h6">Nem található csomag.</Typography>
        <Typography variant="body2">Válassz másik listát, vagy módosítsd a keresést.</Typography>
      </Stack>
    );
  }

  return (
    <TableContainer>
      <Table sx={{ minWidth: 900 }} aria-label="Raktári csomagok">
        <TableHead sx={{ bgcolor: "#f3f6fb" }}>
          <TableRow>
            <TableCell>Csomag</TableCell>
            <TableCell>Feladó és címzett</TableCell>
            <TableCell>Méret</TableCell>
            <TableCell>Állapot</TableCell>
            <TableCell>Futár</TableCell>
            <TableCell align="right">Művelet</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {packages.map((item) => {
            const action = actionFor(item);
            return (
              <TableRow key={item.id} hover>
                <TableCell sx={{ maxWidth: 250 }}>
                  <MuiLink component={Link} to={`/?trackingNumber=${encodeURIComponent(item.trackingNumber)}`}
                    sx={{ display: "block", fontFamily: "monospace", overflowWrap: "anywhere", fontSize: 13 }}>
                    {item.trackingNumber}
                  </MuiLink>
                  <Typography variant="caption" color="text.secondary">
                    Felvétel: {dayjs(item.pickupDate).format("YYYY. MM. DD.")} {item.pickupSlot || ""}
                  </Typography>
                </TableCell>
                <TableCell sx={{ minWidth: 230, maxWidth: 320 }}>
                  <Stack spacing={1}>
                    <Box>
                      <Typography variant="body2" sx={{ fontWeight: 600 }}>{item.senderName}</Typography>
                      <Typography variant="caption" color="text.secondary">{item.senderAddress}</Typography>
                    </Box>
                    <Box>
                      <Typography variant="body2" sx={{ fontWeight: 600 }}>{item.recipientName}</Typography>
                      <Typography variant="caption" color="text.secondary">{item.recipientAddress}</Typography>
                    </Box>
                  </Stack>
                </TableCell>
                <TableCell>
                  <Chip label={item.size} size="small" variant="outlined" />
                  <Typography variant="caption" sx={{ mt: 0.5, display: "block" }}>
                    {(item.weight / 1000).toLocaleString("hu-HU")} kg
                  </Typography>
                </TableCell>
                <TableCell>
                  <Chip label={statusLabels[item.currentStatus] || "Ismeretlen állapot"} size="small"
                    color={item.canReceive ? "success" : "default"} />
                </TableCell>
                <TableCell>{getCourierName(couriers, item.courierId)}</TableCell>
                <TableCell align="right">
                  {action ? (
                    <Button size="small" variant="outlined" disabled={disabled}
                      aria-label={`${action.label}: ${item.trackingNumber}`}
                      onClick={() => onAction(item, action.kind)}>
                      {action.label}
                    </Button>
                  ) : <Typography variant="caption" color="text.secondary">Nincs módosítható feladat</Typography>}
                </TableCell>
              </TableRow>
            );
          })}
        </TableBody>
      </Table>
    </TableContainer>
  );
}
