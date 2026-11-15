import { useState } from "react";
import {
  Alert,
  Dialog,
  DialogContent,
  IconButton,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Tooltip,
  Typography,
} from "@mui/material";
import LocalShipping from "@mui/icons-material/LocalShipping";

import AssignCourierForm from "./AssignCourierForm";
import dayjs from "../utils/dayjs";

const WarehouseList = ({ packages, onChanged }) => {
  const [editedPackage, setEditedPackage] = useState(null);

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
                    <Tooltip title="Futár kiosztása">
                      <IconButton onClick={() => setEditedPackage(item)} aria-label="Futár kiosztása">
                        <LocalShipping color="info" />
                      </IconButton>
                    </Tooltip>
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
                onChanged();
              }}
              onCancel={() => setEditedPackage(null)}
            />
          )}
        </DialogContent>
      </Dialog>
    </Paper>
  );
};

export default WarehouseList;
