import { useEffect, useRef, useState } from "react";
import { Link } from "react-router";
import {
  Alert, Box, Button, Container, Grid, LinearProgress, Paper,
  Stack, Tab, Tabs, TextField, Typography,
} from "@mui/material";
import WarehouseOutlined from "@mui/icons-material/WarehouseOutlined";
import Refresh from "@mui/icons-material/Refresh";
import AutoAwesome from "@mui/icons-material/AutoAwesome";
import {
  assignCourier, assignDeliveries, getWarehouseData, receivePackage,
} from "../../api/warehouseApi";
import CourierAssignmentDialog from "./CourierAssignmentDialog";
import WarehouseConfirmDialog from "./WarehouseConfirmDialog";
import WarehousePackageTable from "./WarehousePackageTable";
import AssignmentResult from "./AssignmentResult";
import { getPackageSpace } from "./warehouseHelpers";

const tabs = ["Raktári készlet", "Felvételi igények", "Futároknál", "Kiszállításra vár"];

function errorMessage(error) {
  if (error.status === 401) return "A munkamenet lejárt. Jelentkezz be újra!";
  if (error.status === 403) return "A művelethez nincs megfelelő jogosultságod.";
  return error.message || "A művelet nem sikerült. Próbáld újra!";
}

export default function WarehouseDashboard() {
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState(null);
  const [revision, setRevision] = useState(0);
  const [tab, setTab] = useState(0);
  const [search, setSearch] = useState("");
  const [selection, setSelection] = useState(null);
  const [busy, setBusy] = useState(false);
  const [actionError, setActionError] = useState("");
  const [success, setSuccess] = useState("");
  const [assignmentResult, setAssignmentResult] = useState(null);
  const savingRef = useRef(false);

  useEffect(() => {
    let cancelled = false;
    getWarehouseData()
      .then((value) => { if (!cancelled) setData(value); })
      .catch((error) => {
        if (!cancelled) { setLoadError(error); setData(null); }
      })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [revision]);

  function refresh() {
    setLoading(true);
    setLoadError(null);
    setRevision((value) => value + 1);
  }

  function openAction(item, kind) {
    setActionError("");
    setSuccess("");
    setSelection({ package: item, kind });
  }

  async function performAction(action, message) {
    if (savingRef.current) return;
    savingRef.current = true;
    setBusy(true);
    setActionError("");
    try {
      const result = await action();
      if (selection.kind === "auto") setAssignmentResult(result);
      setSuccess(message);
      setSelection(null);
      refresh();
    } catch (error) {
      setActionError(errorMessage(error));
      if (error.status === 409 || error.status === 404) refresh();
    } finally {
      savingRef.current = false;
      setBusy(false);
    }
  }

  function saveCourier(courierId) {
    return performAction(
      () => assignCourier(selection.package.id, courierId, selection.kind),
      selection.kind === "reassign" ? "A futárt sikeresen módosítottuk." : "A feladatot kiosztottuk a futárnak.",
    );
  }

  function confirmAction() {
    if (selection.kind === "auto") return performAction(assignDeliveries, "Az automatikus kiosztás befejeződött.");
    return performAction(
      () => receivePackage(selection.package.id, selection.package.courierId),
      "A csomagot sikeresen bevételeztük.",
    );
  }

  const warehouse = data?.warehouse || [];
  const pickups = data?.pickups || [];
  const waiting = data?.waiting || [];
  const assigned = data?.assigned || [];
  const couriers = data?.couriers || [];
  const pickupTasks = [...pickups, ...assigned.filter((item) => item.currentStatus === "AssignedForPickup")];
  const onRoad = assigned.filter((item) => !["AssignedForPickup", "AssignedForDelivery"].includes(item.currentStatus));
  const lists = [warehouse, pickupTasks, onRoad, waiting];
  const query = search.trim().toLocaleLowerCase("hu-HU");
  const filtered = lists[tab].filter((item) => [item.trackingNumber, item.senderName, item.senderAddress, item.recipientName, item.recipientAddress]
    .some((value) => (value || "").toLocaleLowerCase("hu-HU").includes(query)));
  const usedSpace = warehouse.every((item) => getPackageSpace(item.size) > 0)
    ? warehouse.reduce((sum, item) => sum + getPackageSpace(item.size), 0) : null;
  const summaries = [
    { label: "Raktárban", count: warehouse.length, note: usedSpace === null ? "Ismeretlen méretű csomag" : `${usedSpace} helyegység foglalt` },
    { label: "Kiszállításra vár", count: waiting.length, note: "Még nincs futárhoz rendelve" },
    { label: "Kiosztatlan felvétel", count: pickups.length, note: "Futár kijelölésére vár" },
    { label: "Beérkező csomag", count: assigned.filter((item) => item.canReceive).length, note: "Úton a raktárba" },
  ];
  const disabled = loading || busy || !data;

  return (
    <Box sx={{ bgcolor: "#f4f7fb", minHeight: "calc(100vh - 64px)", pb: 5 }}>
      <Box sx={{ bgcolor: "primary.dark", color: "common.white", py: 4 }}>
        <Container maxWidth="xl">
          <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ justifyContent: "space-between", alignItems: { xs: "flex-start", sm: "center" } }}>
            <Box>
              <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
                <WarehouseOutlined />
                <Typography variant="overline">Raktári munkafelület</Typography>
              </Stack>
              <Typography component="h1" variant="h4" sx={{ fontWeight: 700 }}>Raktárkezelés</Typography>
              <Typography sx={{ color: "#dbeafe", mt: 1 }}>Beérkező csomagok, készlet és futárfeladatok egy helyen.</Typography>
            </Box>
            <Button variant="outlined" color="inherit" startIcon={<Refresh />} onClick={refresh} disabled={loading || busy}>Frissítés</Button>
          </Stack>
        </Container>
      </Box>
      <Container maxWidth="xl" sx={{ pt: 3 }}>
        <Stack spacing={3}>
          {loadError && <Alert severity="error" action={loadError.status === 401 ? <Button component={Link} to="/#belepes" color="inherit">Bejelentkezés</Button> : undefined}>{errorMessage(loadError)}</Alert>}
          {success && <Alert severity="success" onClose={() => setSuccess("")}>{success}</Alert>}
          {actionError && !selection && <Alert severity="error">{actionError}</Alert>}
          {loading && <LinearProgress aria-label="Raktári adatok betöltése" />}
          <Grid container spacing={2}>
            {summaries.map((item) => (
              <Grid key={item.label} size={{ xs: 12, sm: 6, lg: 3 }}>
                <Paper variant="outlined" sx={{ p: 2.5, borderRadius: 3, height: "100%", boxSizing: "border-box" }}>
                  <Typography variant="body2" color="text.secondary">{item.label}</Typography>
                  <Typography variant="h4" color="primary.dark" sx={{ my: 1, fontWeight: 700 }}>{loading ? "—" : data ? item.count : "—"}</Typography>
                  <Typography variant="caption" color="text.secondary">{loading ? "Adatok betöltése…" : data ? item.note : "Az adatok nem érhetők el."}</Typography>
                </Paper>
              </Grid>
            ))}
          </Grid>
          {assignmentResult && <AssignmentResult result={assignmentResult} couriers={couriers} onClose={() => setAssignmentResult(null)} />}
          <Paper variant="outlined" sx={{ borderRadius: 3, overflow: "hidden" }}>
            <Stack direction={{ xs: "column", md: "row" }} spacing={2} sx={{ p: 2.5, justifyContent: "space-between" }}>
              <Box>
                <Typography component="h2" variant="h6">Csomagok kezelése</Typography>
                <Typography variant="body2" color="text.secondary">A kiosztott kiszállítás a futár indulásáig a raktári készlet része.</Typography>
              </Box>
              <Button variant="contained" startIcon={<AutoAwesome />} disabled={disabled || waiting.length === 0}
                onClick={() => openAction(null, "auto")} sx={{ whiteSpace: "nowrap", alignSelf: "center" }}>
                Automatikus kiosztás
              </Button>
            </Stack>
            <Tabs value={tab} onChange={(_, value) => setTab(value)} variant="scrollable" scrollButtons="auto" aria-label="Raktári listák" sx={{ borderBottom: 1, borderColor: "divider", px: 1 }}>
              {tabs.map((label, index) => <Tab key={label} id={`warehouse-tab-${index}`} aria-controls="warehouse-panel" label={`${label} (${lists[index].length})`} />)}
            </Tabs>
            <Box role="tabpanel" id="warehouse-panel" aria-labelledby={`warehouse-tab-${tab}`}>
              <Box sx={{ px: 2.5, py: 2 }}>
                <TextField label="Keresés a listában" placeholder="Csomagazonosító, név vagy cím" size="small" value={search}
                  onChange={(event) => setSearch(event.target.value)} sx={{ width: { xs: "100%", sm: 380 } }} />
              </Box>
              {!loading && data && <WarehousePackageTable packages={filtered} couriers={couriers} waitingIds={new Set(waiting.map((item) => item.id))} disabled={disabled} onAction={openAction} />}
            </Box>
          </Paper>
        </Stack>
      </Container>
      {selection && ["pickup", "delivery", "reassign"].includes(selection.kind) && (
        <CourierAssignmentDialog selection={selection} couriers={couriers} busy={busy} error={actionError}
          onClose={() => setSelection(null)} onConfirm={saveCourier} />
      )}
      {selection && ["receive", "auto"].includes(selection.kind) && (
        <WarehouseConfirmDialog selection={selection} couriers={couriers} waitingCount={waiting.length} busy={busy} error={actionError}
          onClose={() => setSelection(null)} onConfirm={confirmAction} />
      )}
    </Box>
  );
}
