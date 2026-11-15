import { useEffect, useState } from "react";
import { Link } from "react-router";
import {
  Alert,
  Button,
  Chip,
  Typography,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
} from "@mui/material";

import { endpoints, getData } from "../api/apiCalls";
import { PACKAGE_STATUS_COLORS, PACKAGE_STATUS_LABELS } from "../constants/constants";
import { useAuth } from "../provider/AuthContext";
import dayjs from "../utils/dayjs";

const PackageList = () => {
  const { setAlert } = useAuth();
  const [packages, setPackages] = useState([]);

  useEffect(() => {
    getData(endpoints.getAllByUser)
      .then(setPackages)
      .catch((error) => setAlert({ message: error.message, severity: "error" }))
  }, [setAlert]);

  return (
    <Paper sx={{ p: 4, my: 4 }}>
      <Typography variant="h5" sx={{ mb: 4 }}>
        Csomagjaim
      </Typography>
      {packages.length === 0 ? (
        <Stack spacing={4} sx={{ alignItems: "flex-start", }}>
          <Alert severity="info" sx={{width: '90%'}}>Még a rendszerben regisztrált csomagod.</Alert>
          <Button component={Link} to="/csomagfeladas" variant="contained">
            Csomag feladása
          </Button>
        </Stack>)
        :
        (
          <TableContainer>
            <Table>
              <TableHead>
                <TableRow>
                  <TableCell>Követési szám</TableCell>
                  <TableCell>Irány</TableCell>
                  <TableCell>Feladó</TableCell>
                  <TableCell>Címzett</TableCell>
                  <TableCell>Cím</TableCell>
                  <TableCell>Méret</TableCell>
                  <TableCell>Felvétel</TableCell>
                  <TableCell>Státusz</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {packages.map((item) => (
                  <TableRow key={item.id}>
                    <TableCell>
                      <Link to={`/?trackingNumber=${item.trackingNumber}`}>
                        {item.trackingNumber}
                      </Link>
                    </TableCell>
                    <TableCell>
                      <Chip
                        size="small"
                        label={item.isSender ? "Feladott" : "Érkezett"}
                        color={item.isSender ? "primary" : "default"}
                      />
                    </TableCell>
                    <TableCell>{item.senderName}</TableCell>
                    <TableCell>{item.recipientName}</TableCell>
                    <TableCell>{item.recipientAddress}</TableCell>
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
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>)
      }</Paper>
  );
};

export default PackageList;
