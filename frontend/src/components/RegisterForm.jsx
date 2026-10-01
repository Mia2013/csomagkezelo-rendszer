import { useRef, useState } from "react";
import {
  Box,
  Button,
  Container,
  FormControl,
  IconButton,
  InputAdornment,
  InputLabel,
  OutlinedInput,
  Paper,
  TextField,
  Typography,
} from "@mui/material";
import VisibilityOff from "@mui/icons-material/VisibilityOff";
import Visibility from "@mui/icons-material/Visibility";
import Send from "@mui/icons-material/Send";

import { useAuth } from "../provider/AuthProvider";
import ValidationCaption from "./ValidationCaption";

const RegisterForm = () => {
  const { register } = useAuth();

  const [showPassword, setShowPassword] = useState(false);
  const [validationErrors, setValidationErrors] = useState({});

  const userNameRef = useRef();
  const emailRef = useRef();
  const passwordRef = useRef();
  const confirmPasswordRef = useRef();
  const lastNameRef = useRef();
  const firstNameRef = useRef();
  const addressRef = useRef();
  const phoneNumberRef = useRef();

  const handleClickShowPassword = () => setShowPassword((show) => !show);

  const validateForm = (data, confirmPassword) => {
    const errors = {};

    if (!data.userName.trim()) errors.userName = "Felhasználónév kötelező!";
    if (!data.email.trim()) errors.email = "Email cím kötelező!";
    if (!data.password.trim()) errors.password = "Jelszó megadása kötelező!";
    if (data.password !== confirmPassword) {
      errors.confirmPassword = "A jelszavak nem egyeznek!";
    }
    if (!data.lastName.trim()) errors.lastName = "Vezetéknév kötelező!";
    if (!data.firstName.trim()) errors.firstName = "Keresztnév kötelező!";
    if (!data.address.trim()) errors.address = "Lakcím kötelező!";

    return errors;
  };

  const handleSubmit = async (e) => {
    e.preventDefault();

    const formData = {
      userName: userNameRef.current.value,
      email: emailRef.current.value,
      password: passwordRef.current.value,
      lastName: lastNameRef.current.value,
      firstName: firstNameRef.current.value,
      address: addressRef.current.value,
      phoneNumber: phoneNumberRef.current.value,
    };

    const errors = validateForm(formData, confirmPasswordRef.current.value);
    if (Object.keys(errors).length > 0) {
      setValidationErrors(errors);
      return;
    }
    setValidationErrors({});
    await register(formData);
  };

  return (
    <Container maxWidth="sm">
      <Paper sx={{ p: 4, my: 4 }}>
        <Typography variant="h5" component="h1" gutterBottom align="center">
          Regisztráció
        </Typography>

        <Box
          component="form"
          onSubmit={handleSubmit}
          sx={{ display: "flex", flexDirection: "column", gap: 2, mt: 2 }}
        >
          <TextField
            fullWidth
            label="Felhasználónév*"
            inputRef={userNameRef}
            error={!!validationErrors.userName}
            helperText={validationErrors.userName}
          />
          <Box sx={{ display: "flex", gap: 2 }}>
            <TextField
              fullWidth
              label="Email cím*"
              type="email"
              inputRef={emailRef}
              error={!!validationErrors.email}
              helperText={validationErrors.email}
            />
            <TextField
              fullWidth
              label="Telefonszám"
              type="tel"
              inputRef={phoneNumberRef}
            />
          </Box>

          <Box sx={{ display: "flex", gap: 2 }}>
            <FormControl
              variant="outlined"
              fullWidth
              error={!!validationErrors.password}
            >
              <InputLabel>Jelszó*</InputLabel>
              <OutlinedInput
                inputRef={passwordRef}
                type={showPassword ? "text" : "password"}
                label="Jelszó*"
                endAdornment={
                  <InputAdornment position="end">
                    <IconButton onClick={handleClickShowPassword} edge="end">
                      {showPassword ? <VisibilityOff /> : <Visibility />}
                    </IconButton>
                  </InputAdornment>
                }
              />
              {validationErrors.password && (
                <ValidationCaption message={validationErrors.password} />
              )}
            </FormControl>

            <FormControl
              variant="outlined"
              fullWidth
              error={!!validationErrors.confirmPassword}
            >
              <InputLabel>Jelszó újra*</InputLabel>
              <OutlinedInput
                inputRef={confirmPasswordRef}
                type={showPassword ? "text" : "password"}
                label="Jelszó újra*"
              />
              {validationErrors.confirmPassword && (
                <ValidationCaption message={validationErrors.confirmPassword} />
              )}
            </FormControl>
          </Box>
          <Box sx={{ display: "flex", gap: 2 }}>
            <TextField
              fullWidth
              label="Vezetéknév*"
              inputRef={lastNameRef}
              error={!!validationErrors.lastName}
              helperText={validationErrors.lastName}
            />
            <TextField
              fullWidth
              label="Keresztnév*"
              inputRef={firstNameRef}
              error={!!validationErrors.firstName}
              helperText={validationErrors.firstName}
            />
          </Box>

          <TextField
            fullWidth
            label="Lakcím*"
            inputRef={addressRef}
            error={!!validationErrors.address}
            helperText={validationErrors.address}
          />

          <Button
            type="submit"
            variant="contained"
            startIcon={<Send />}
            sx={{ py: 1.5, mt: 1 }}
          >
            Fiók létrehozása
          </Button>
        </Box>
      </Paper>
    </Container>
  );
};

export default RegisterForm;
