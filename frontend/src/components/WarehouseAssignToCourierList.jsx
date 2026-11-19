import { useEffect, useState } from "react";
import {
  Alert,
  Button,
  Dialog,
  DialogContent,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
} from "@mui/material";
import LocalShipping from "@mui/icons-material/LocalShipping";

import { endpoints, getData } from "../api/apiCalls";
import { useAuth } from "../provider/AuthContext";
import AssignCourierForm from "./AssignCourierForm";
import dayjs from "../utils/dayjs";

const WarehouseAssignToCourierList = ({ onLoaded }) => {
  const { setAlert } = useAuth();
  const [packages, setPackages] = useState([]);
  const [editedPackage, setEditedPackage] = useState(null);

  const loadPackages = () =>
    getData(endpoints.unassignedPrebooked)
      .then((data) => {
        setPackages(data);
        onLoaded?.(data.length);
      })
      .catch((error) => setAlert({ message: error.message, severity: "error" }));

  useEffect(() => {
    loadPackages();
  }, [setAlert]);

  return (
    <Paper sx={{ p: 4 }}>
      <Typography variant="h5" sx={{ mb: 4 }}>
        Felvételre váró csomagok
      </Typography>
      {packages.length === 0 ? (
        <Alert severity="info">Nincs felvételre váró csomag.</Alert>
      ) : (
        <TableContainer>
          <Table>
            <TableHead>
              <TableRow sx={{ bgcolor: "#e3f2fd" }}>
                <TableCell>Követési szám</TableCell>
                <TableCell>Feladó</TableCell>
                <TableCell>Feladó címe</TableCell>
                <TableCell>Méret</TableCell>
                <TableCell>Felvétel</TableCell>
                <TableCell>Művelet</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {packages.map((item) => (
                <TableRow key={item.id}>
                  <TableCell>{item.trackingNumber}</TableCell>
                  <TableCell>{item.senderName}</TableCell>
                  <TableCell>{item.senderAddress}</TableCell>
                  <TableCell>{item.size}</TableCell>
                  <TableCell>
                    {dayjs(item.pickupDate).format("YYYY. MM. DD.")} {item.pickupSlot}
                  </TableCell>
                  <TableCell>
                    <Button
                      variant="contained"
                      size="small"
                      color="info"
                      startIcon={<LocalShipping />}
                      onClick={() => setEditedPackage(item)}
                    >
                      Futárnak kiosztás
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}
      <Dialog open={!!editedPackage} onClose={() => setEditedPackage(null)} maxWidth="sm" fullWidth>
        <DialogContent>
          {editedPackage && (
            <AssignCourierForm
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

export default WarehouseAssignToCourierList;
