import { Box, Button, Stack, TextField, Typography } from "@mui/material";
import Save from "@mui/icons-material/Save";
import Close from "@mui/icons-material/Close";

import { endpoints, putData } from "../../api/apiCalls";
import { useAuth } from "../../provider/AuthContext";

const AssignCourierForm = ({ item, type, onSaved, onCancel }) => {
  const { setAlert } = useAuth();
  const isPickup = type === "pickup";
  const endpoint = isPickup ? endpoints.assignforpickup : endpoints.assignfordelivery;

  const handleSubmit = async (e) => {
    e.preventDefault();
    const data = Object.fromEntries(new FormData(e.currentTarget));
    try {
      await putData(`${endpoint}/${item.id}`, { courierId: data.courierId });
      setAlert({ message: "A futár hozzá lett rendelve a csomaghoz.", severity: "success" });
      onSaved();
    } catch (error) {
      setAlert({ message: error.message, severity: "error" });
    }
  };

  return (
    <Box component="form" onSubmit={handleSubmit}>
      <Typography variant="h5" gutterBottom>
        {isPickup ? "Felvétel kiosztása" : "Kiszállítás kiosztása"}
      </Typography>
      <Stack spacing={2}>
        <TextField
          label="Futár azonosító*"
          name="courierId"
          type="number"
          required
          fullWidth
        />
        <Stack direction="row" spacing={2} sx={{ justifyContent: "flex-end" }}>
          <Button type="submit" variant="contained" startIcon={<Save />}>
            Mentés
          </Button>
          <Button color="inherit" startIcon={<Close />} onClick={onCancel}>
            Mégse
          </Button>
        </Stack>
      </Stack>
    </Box>
  );
};

export default AssignCourierForm;
