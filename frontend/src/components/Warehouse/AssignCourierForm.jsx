import { useEffect, useRef, useState } from "react";
import {
  Alert,
  Box,
  Button,
  MenuItem,
  TextField,
  Typography,
} from "@mui/material";
import Save from "@mui/icons-material/Save";
import Cancel from "@mui/icons-material/Cancel";

import { endpoints, getData, putData } from "../../api/apiCalls";
import { useAuth } from "../../provider/AuthContext";

const AssignCourierForm = ({ item, type, onSaved, onCancel }) => {
  const { setAlert } = useAuth();

  const [couriers, setCouriers] = useState([]);
  const [validationErrors, setValidationErrors] = useState({});

  const courierRef = useRef();

  const isPickup = type === "pickup";

  const getCouriers = async () => {
    try {
      const data = await getData(endpoints.couriers);
      setCouriers(data);
    } catch (error) {
      setAlert({ message: error.message, severity: "error" });
    }
  };

  useEffect(() => {
    getCouriers();
  }, []);

  const validateForm = (data) => {
    const errors = {};

    if (!data.courierId) {
      errors.courierId = "Futár kiválasztása kötelező!";
    }

    return errors;
  };

  const handleSubmit = async (e) => {
    e.preventDefault();

    const formData = {
      courierId: courierRef.current.value,
    };

    const errors = validateForm(formData);
    if (Object.keys(errors).length > 0) {
      setValidationErrors(errors);
      return;
    }
    setValidationErrors({});

    const endpoint = isPickup
      ? endpoints.assignforpickup
      : endpoints.assignfordelivery;

    try {
      await putData(`${endpoint}/${item.id}`, formData);
      setAlert({
        message: "A futár hozzá lett rendelve a csomaghoz.",
        severity: "success",
      });
      onSaved();
    } catch (error) {
      setAlert({ message: error.message, severity: "error" });
    }
  };

  return (
    <Box
      component="form"
      onSubmit={handleSubmit}
      sx={{ display: "flex", flexDirection: "column", gap: 2 }}
    >
      <Typography variant="h5">
        {isPickup ? "Felvétel kiosztása" : "Kiszállítás kiosztása"}
      </Typography>

      {couriers.length === 0 ? (
        <Alert severity="info">Nincs futár a rendszerben.</Alert>
      ) : (
        <TextField
          select
          fullWidth
          label="Futár kiválasztása*"
          defaultValue=""
          inputRef={courierRef}
          error={!!validationErrors.courierId}
          helperText={validationErrors.courierId}
        >
          {couriers.map((courier) => (
            <MenuItem key={courier.id} value={courier.id}>
              {courier.fullName}
            </MenuItem>
          ))}
        </TextField>
      )}

      <Box sx={{ display: "flex", gap: 2, justifyContent: "flex-end" }}>
        <Button
          type="submit"
          variant="contained"
          startIcon={<Save />}
          disabled={couriers.length === 0}
        >
          Mentés
        </Button>
        <Button
          color="inherit"
          variant="contained"
          startIcon={<Cancel />}
          onClick={onCancel}
        >
          Mégse
        </Button>
      </Box>
    </Box>
  );
};

export default AssignCourierForm;
