import { useEffect, useState } from "react";
import { Link } from "react-router";
import {
  Alert,
  Button,
  Chip,
  Dialog,
  DialogContent,
  IconButton,
  Tooltip,
  Typography,
  Paper,
  Stack,
  Tab,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Tabs,
} from "@mui/material";

import Edit from "@mui/icons-material/Edit";
import { endpoints, getData } from "../api/apiCalls";
import PackageUpdateForm from "./PackageUpdateForm";
import { PACKAGE_STATUS_COLORS, PACKAGE_STATUS_LABELS } from "../constants/constants";
import { useAuth } from "../provider/AuthContext";
import dayjs from "../utils/dayjs";

const PackageListUser = () => {
  const { setAlert } = useAuth();
  const [packages, setPackages] = useState([]);
  const [tab, setTab] = useState(0);
  const [editedPackage, setEditedPackage] = useState(null);
  const sentPackages = packages.filter((item) => item.isSender);
  const receivedPackages = packages.filter((item) => !item.isSender);
  let sentPackagesTab = tab === 0 ? true : false;
  const shownPackages = sentPackagesTab ? sentPackages : receivedPackages;

  const loadPackages = () =>
    getData(endpoints.getAllByUser)
      .then(setPackages)
      .catch((error) => setAlert({ message: error.message, severity: "error" }));

  useEffect(() => {
    loadPackages();
  }, [setAlert]);


  const getEditable = (status, pickupDate) => {
    if (status === "PreBooked" && dayjs(pickupDate).isAfter(dayjs(), "day")) {
      return true;
    }
    return false; 
  }

  return (
    <Paper sx={{ p: 4, my: 4 }}>
      <Typography variant="h5" sx={{ mb: 4 }}>
        Csomagjaim
      </Typography>
      {packages.length === 0 ? (
        <Stack spacing={4} sx={{ alignItems: "flex-start" }}>
          <Alert severity="info" sx={{ width: "90%" }}>Még a rendszerben regisztrált csomagod.</Alert>
          <Button component={Link} to="/csomagfeladas" variant="contained">
            Csomag feladása
          </Button>
        </Stack>
      ) : (
        <>
          <Tabs value={tab} onChange={(e, value) => setTab(value)} sx={{ mb: 2 }}>
            <Tab label={`Feladott (${sentPackages.length})`} value={0} />
            <Tab label={`Érkezett (${receivedPackages.length})`} value={1} />
          </Tabs>
          {shownPackages.length === 0 ? (
            <Alert severity="info">
              {sentPackagesTab ? "Még nincs feladott csomagod." : "Még nincs érkezett csomagod."}
            </Alert>
          ) : (
            <TableContainer>
              <Table>
                <TableHead>
                  <TableRow>
                    <TableCell>Követési szám</TableCell>
                    <TableCell>{sentPackagesTab ? "Címzett" : "Feladó"}</TableCell>
                    <TableCell>Méret</TableCell>
                    <TableCell>Felvétel</TableCell>
                    <TableCell>Státusz</TableCell>
                    {sentPackagesTab && <TableCell>Művelet</TableCell>}
                  </TableRow>
                </TableHead>
                <TableBody>
                  {shownPackages.map((item) => (
                    <TableRow key={item.id}>
                      <TableCell>
                        <Link to={`/?trackingNumber=${item.trackingNumber}`}>
                          {item.trackingNumber}
                        </Link>
                      </TableCell>
                      <TableCell>{sentPackagesTab ? item.recipientName : item.senderName}</TableCell>
                      <TableCell>{item.size}</TableCell>
                      <TableCell>
                        {dayjs(item.pickupDate).format("YYYY. MM. DD.")} {item.pickupSlot}
                      </TableCell>
                      <TableCell>
                        <Chip
                          size="small"
                          label={PACKAGE_STATUS_LABELS[item.currentStatus]}
                          color={PACKAGE_STATUS_COLORS[item.currentStatus]}
                        />
                      </TableCell>
                      {sentPackagesTab && (
                        <TableCell>
                          {getEditable(item.currentStatus, item.pickupDate) && (
                            <Tooltip title="Módosítás">
                              <IconButton onClick={() => setEditedPackage(item)} aria-label="Csomag módosítása">
                                <Edit color="info" />
                              </IconButton>
                            </Tooltip>
                          )}
                        </TableCell>
                      )}
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          )}
        </>
      )}
      <Dialog open={!!editedPackage} onClose={() => setEditedPackage(null)} maxWidth="sm" fullWidth>
        <DialogContent>
          {editedPackage && (
            <PackageUpdateForm
              item={editedPackage}
              onSaved={() => {
                setEditedPackage(null);
                loadPackages();
              }}
              onCancel={() => setEditedPackage(null)}
            />
          )}
        </DialogContent>
      </Dialog>
    </Paper>
  );
};

export default PackageListUser;
