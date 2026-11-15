import { Box, Button, MenuItem, Stack, TextField, Typography } from "@mui/material";
import Save from "@mui/icons-material/Save";
import Cancel from "@mui/icons-material/Cancel";

import { endpoints, putData } from "../api/apiCalls";
import { useAuth } from "../provider/AuthContext";

const PackageUpdateForm = ({ item, onSaved, onCancel }) => {
  const { setAlert } = useAuth();

  const handleSubmit = async (e) => {
    e.preventDefault();
    const data = Object.fromEntries(new FormData(e.currentTarget));
    try {
      await putData(`${endpoints.updatePackage}/${item.id}`, data);
      setAlert({ message: "A csomag adatai frissítve.", severity: "success" });
      onSaved();
    } catch (error) {
      setAlert({ message: error.message, severity: "error" });
    }
  };

  return (
    <Box component="form" onSubmit={handleSubmit}>
      <Typography variant="h5" sx={{mb: 2}}>
        <Typography variant="h5" sx={{ fontStyle: 'italic', display: 'inline'}}>{item.trackingNumber}</Typography> csomag módosítása
      </Typography>
      <Stack spacing={2}>
        <TextField
          label="Címzett címe*"
          name="recipientAddress"
          defaultValue={item.recipientAddress}
          required
          fullWidth
        />
        <TextField select label="Méret" name="size" defaultValue={item.size} fullWidth>
          <MenuItem value="S">S</MenuItem>
          <MenuItem value="M">M</MenuItem>
          <MenuItem value="L">L</MenuItem>
        </TextField>
        <TextField select label="Idősáv" name="pickupSlot" defaultValue={item.pickupSlot} fullWidth>
          <MenuItem value="09:00-12:00">09:00-12:00</MenuItem>
          <MenuItem value="12:00-16:00">12:00-16:00</MenuItem>
        </TextField>
        <Stack direction="row" spacing={2} sx={{ justifyContent: "flex-end" }}>
          <Button type="submit" variant="contained" startIcon={<Save />}>
            Mentés
          </Button>
          <Button color="inherit" variant="contained" startIcon={<Cancel />} onClick={onCancel}>
            Mégse
          </Button>
        </Stack>
      </Stack>
    </Box>
  );
};

export default PackageUpdateForm;
