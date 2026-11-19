import { useEffect, useState } from "react";
import {
  Alert,
  Button,
  Chip,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
} from "@mui/material";
import MoveToInbox from "@mui/icons-material/MoveToInbox";

import { endpoints, getData, putData } from "../../api/apiCalls";
import { PACKAGE_STATUS_COLORS, PACKAGE_STATUS_LABELS } from "../../constants/constants";
import { useAuth } from "../../provider/AuthContext";

const WarehouseGetPackagesByStatus = ({ status, title, onLoaded }) => {
  const { setAlert } = useAuth();
  const [packages, setPackages] = useState([]);
  const canReceive = status === "InTransitToWarehouse";

  const loadPackages = () =>
    getData(endpoints.warehousePackages, { status })
      .then((data) => {
        setPackages(data);
        onLoaded?.(data.length);
      })
      .catch((error) => setAlert({ message: error.message, severity: "error" }));

  useEffect(() => {
    loadPackages();
  }, [status, setAlert]);

  const receivePackage = async (item) => {
    try {
      await putData(`${endpoints.receivetowarehouse}/${item.id}`);
      setAlert({ message: `A(z) ${item.trackingNumber} csomag bevételezve.`, severity: "success" });
      loadPackages();
    } catch (error) {
      setAlert({ message: error.message, severity: "error" });
    }
  };

  return (
    <Paper sx={{ p: 2 }}>
      <Typography variant="h5" sx={{ mb: 4 }}>
        {title}
      </Typography>
      {packages.length === 0 ? (
        <Alert severity="info">Nincs {PACKAGE_STATUS_LABELS[status].toLowerCase()} csomag.</Alert>
      ) : (
        <TableContainer>
          <Table>
            <TableHead>
              <TableRow sx={{ bgcolor: "#e3f2fd" }}>
                <TableCell>Státusz</TableCell>
                <TableCell>Követési szám</TableCell>
                <TableCell>Futár</TableCell>
                <TableCell>Feladó címe</TableCell>
                <TableCell>Címzett címe</TableCell>
                <TableCell>Méret</TableCell>
                {canReceive && <TableCell>Művelet</TableCell>}
              </TableRow>
            </TableHead>
            <TableBody>
              {packages.map((item) => (
                <TableRow key={item.id}>
                  <TableCell>
                    <Chip
                      size="small"
                      variant="outlined"
                      label={PACKAGE_STATUS_LABELS[item.currentStatus]}
                      color={PACKAGE_STATUS_COLORS[item.currentStatus]}
                    />
                  </TableCell>
                  <TableCell>{item.trackingNumber}</TableCell>
                  <TableCell>{item.courierName ?? "–"}</TableCell>
                  <TableCell>{item.senderAddress}</TableCell>
                  <TableCell>{item.recipientAddress}</TableCell>
                  <TableCell>{item.size}</TableCell>
                  {canReceive && (
                    <TableCell>
                      <Button
                        variant="contained"
                        size="small"
                        startIcon={<MoveToInbox />}
                        onClick={() => receivePackage(item)}
                      >
                        Bevételezés
                      </Button>
                    </TableCell>
                  )}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}
    </Paper>
  );
};

export default WarehouseGetPackagesByStatus;
